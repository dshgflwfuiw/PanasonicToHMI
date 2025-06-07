
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
public class CsvProcessor
{
    // 前缀映射
    private static readonly Dictionary<string, string> PrefixMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DWR", "WR" }, { "DSV", "SV" }, { "DEV", "EV" }, { "DLD", "LD" },
        { "DWX", "WX" }, { "DWY", "WY" }, { "DWL", "WL" }, { "DFL", "FL" },
        { "DDT", "DT" }
    };

    /// <summary>
    /// 智能编码检测+转换主方法
    /// </summary>
    public bool ConvertPlcCsvToHmiCsv(string plcFilepath, string hmiFilepath, Action<string>? log = null)
    {
        if (!File.Exists(plcFilepath))
        {
            log?.Invoke($"文件 '{plcFilepath}' 不存在。");
            return false;
        }

        var hmiDataRows = new List<string[]>();
        string[] hmiHeader = { "名称", "品牌", "起始符号", "起始地址", "", "数据类型" };
        hmiDataRows.Add(hmiHeader);

        Encoding[] encodingsToTry = {
            Encoding.GetEncoding("GBK"),
            Encoding.GetEncoding("gb2312"),
            new UTF8Encoding(true),
            new UTF8Encoding(false),
            Encoding.GetEncoding("big5"),
            Encoding.GetEncoding("iso-8859-1")
        };

        bool fileOpenedSuccessfully = false;
        List<string>? fileLines = null;

        foreach (var encoding in encodingsToTry)
        {
            try
            {
                log?.Invoke($"尝试使用编码 '{encoding.WebName}' 打开源文件。");
                var tempLines = File.ReadAllLines(plcFilepath, encoding);
                if (tempLines.Length > 0 && Regex.IsMatch(tempLines[0], @"[\u4e00-\u9fa5]"))
                {
                    log?.Invoke($"编码 '{encoding.WebName}' 验证成功：检测到中文字符。");
                    fileLines = new List<string>(tempLines);
                    fileOpenedSuccessfully = true;
                    break;
                }
                else if (tempLines.Length <= 1)
                {
                    log?.Invoke($"文件为空或行数过少，假定编码 '{encoding.WebName}' 正确。");
                    fileLines = new List<string>(tempLines);
                    fileOpenedSuccessfully = true;
                    break;
                }
                else
                {
                    log?.Invoke($"编码 '{encoding.WebName}' 读取成功但内容验证失败，继续尝试...");
                }
            }
            catch
            {
                log?.Invoke($"编码 '{encoding.WebName}' 解码失败，尝试下一个...");
            }
        }

        if (!fileOpenedSuccessfully || fileLines == null)
        {
            log?.Invoke("无法找到合适的编码来正确读取源文件。");
            return false;
        }

        if (fileLines.Count > 1)
        {
            fileLines.RemoveAt(0); // 跳过表头
            int rowCount = 0;
            foreach (var line in fileLines)
            {
                rowCount++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] plcRow = line.Split(';');
                if (plcRow.Length < 6)
                {
                    log?.Invoke($"跳过第 {rowCount} 行，因格式不正确或列数不足: {line}");
                    continue;
                }

                string varName = plcRow[1].Trim();
                string simplifiedPlcAddr = plcRow[3].Trim();
                string dataType = plcRow[4].Trim();
                string hmiBrand = "Panasonic FP/KW";
                string hmiSymbol = "UNKNOWN";
                string hmiAddress = simplifiedPlcAddr;

                Match match = Regex.Match(simplifiedPlcAddr, @"([A-Za-z]+)([0-9A-Fa-f]+)");
                if (match.Success)
                {
                    string originalPrefix = match.Groups[1].Value.ToUpper();
                    hmiAddress = match.Groups[2].Value;
                    hmiSymbol = PrefixMapping.ContainsKey(originalPrefix) ? PrefixMapping[originalPrefix] : originalPrefix;
                }
                else
                {
                    log?.Invoke($"无法解析变量 '{varName}' 的地址 '{simplifiedPlcAddr}'。");
                }

                string hmiDataType = dataType.Contains("BOOL") && dataType.Length > 4 ? "BOOL" : dataType;
                hmiDataRows.Add(new string[] { varName, hmiBrand, hmiSymbol, hmiAddress, "", hmiDataType });
            }
        }

        // 检查是否有有效数据
        if (hmiDataRows.Count == 1)
        {
            log?.Invoke("全部数据因格式不正确或列数不足被跳过，未生成输出文件。");
            return false;
        }

        try
        {
            using (StreamWriter sw = new(hmiFilepath, false, new UTF8Encoding(true)))
            {
                foreach (var row in hmiDataRows)
                    sw.WriteLine(string.Join(",", row.Select(s => $"\"{s}\"")));
            }
            log?.Invoke($"目标文件已成功写入: '{hmiFilepath}'");
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke($"写入目标文件时发生错误: {ex.Message}");
            return false;
        }
    }
}
