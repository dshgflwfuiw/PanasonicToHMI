using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class CsvProcessor
{
    // 前缀映射 - 松下到威纶通
    private static readonly Dictionary<string, string> PrefixMappingWeinview = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DWR", "WR" }, { "DSV", "SV" }, { "DEV", "EV" }, { "DLD", "LD" },
        { "DWX", "WX" }, { "DWY", "WY" }, { "DWL", "WL" }, { "DFL", "FL" },
        { "DDT", "DT" }
    };

    // 西门子前缀映射
    private static readonly Dictionary<string, string> PrefixMappingSiemens = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DWR", "DB" }, { "DSV", "DBD" }, { "DEV", "DBW" }, { "DLD", "DBB" },
        { "DWX", "IW" }, { "DWY", "QW" }, { "DWL", "M" }, { "DFL", "MD" },
        { "DDT", "DBD" }
    };

    // 三菱前缀映射
    private static readonly Dictionary<string, string> PrefixMappingMitsubishi = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DWR", "D" }, { "DSV", "SD" }, { "DEV", "R" }, { "DLD", "SM" },
        { "DWX", "X" }, { "DWY", "Y" }, { "DWL", "M" }, { "DFL", "L" },
        { "DDT", "D" }
    };

    // 欧姆龙前缀映射
    private static readonly Dictionary<string, string> PrefixMappingOmron = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DWR", "W" }, { "DSV", "D" }, { "DEV", "E" }, { "DLD", "A" },
        { "DWX", "0.00" }, { "DWY", "100.00" }, { "DWL", "M" }, { "DFL", "T" },
        { "DDT", "D" }
    };

    // 台达前缀映射
    private static readonly Dictionary<string, string> PrefixMappingDelta = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DWR", "D" }, { "DSV", "S" }, { "DEV", "E" }, { "DLD", "M" },
        { "DWX", "X" }, { "DWY", "Y" }, { "DWL", "M" }, { "DFL", "T" },
        { "DDT", "D" }
    };

    /// <summary>
    /// 智能编码检测 + 转换主方法（带进度和错误处理）
    /// </summary>
    public bool ConvertPlcCsvToHmiCsv(
        string plcFilepath, 
        string hmiFilepath, 
        Action<string>? log = null,
        Action<int>? progressCallback = null,
        Action<string>? errorCallback = null,
        Action<int, int>? statsCallback = null,
        string brandCode = "Panasonic FP/KW")
    {
        if (!File.Exists(plcFilepath))
        {
            log?.Invoke($"文件 '{plcFilepath}' 不存在。");
            return false;
        }

        var hmiDataRows = new List<string[]>();
        var errorRowsList = new List<string>();
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

        // 选择对应品牌的前缀映射表
        var currentPrefixMapping = brandCode switch
        {
            "Siemens" => PrefixMappingSiemens,
            "Mitsubishi" => PrefixMappingMitsubishi,
            "Omron" => PrefixMappingOmron,
            "Delta" => PrefixMappingDelta,
            _ => PrefixMappingWeinview // 默认威纶通/松下
        };

        string hmiBrand = brandCode switch
        {
            "Siemens" => "Siemens",
            "Mitsubishi" => "Mitsubishi",
            "Omron" => "Omron",
            "Delta" => "Delta",
            _ => "Panasonic FP/KW"
        };

        int totalRows = 0;
        int successCount = 0;

        if (fileLines.Count > 1)
        {
            fileLines.RemoveAt(0); // 跳过表头
            totalRows = fileLines.Count;
            int rowCount = 0;
            
            foreach (var line in fileLines)
            {
                rowCount++;
                
                // 更新进度
                int progress = (int)((rowCount * 100.0) / totalRows);
                progressCallback?.Invoke(progress);

                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] plcRow = line.Split(';');
                if (plcRow.Length < 6)
                {
                    string errorMsg = $"跳过第 {rowCount} 行，因格式不正确或列数不足：{line}";
                    log?.Invoke(errorMsg);
                    errorRowsList.Add($"第{rowCount}行|格式错误|{line}");
                    errorCallback?.Invoke(errorMsg);
                    continue;
                }

                string varName = plcRow[1].Trim();
                string simplifiedPlcAddr = plcRow[3].Trim();
                string dataType = plcRow[4].Trim();
                string hmiSymbol = "UNKNOWN";
                string hmiAddress = simplifiedPlcAddr;

                Match match = Regex.Match(simplifiedPlcAddr, @"([A-Za-z]+)([0-9A-Fa-f]+)");
                if (match.Success)
                {
                    string originalPrefix = match.Groups[1].Value.ToUpper();
                    hmiAddress = match.Groups[2].Value;
                    
                    if (currentPrefixMapping.ContainsKey(originalPrefix))
                    {
                        hmiSymbol = currentPrefixMapping[originalPrefix];
                    }
                    else
                    {
                        hmiSymbol = originalPrefix;
                        log?.Invoke($"警告：未知前缀 '{originalPrefix}'，保持原样");
                    }
                }
                else
                {
                    string errorMsg = $"无法解析变量 '{varName}' 的地址 '{simplifiedPlcAddr}'。";
                    log?.Invoke(errorMsg);
                    errorRowsList.Add($"第{rowCount}行|地址解析失败|{line}");
                    errorCallback?.Invoke(errorMsg);
                    continue;
                }

                string hmiDataType = dataType.Contains("BOOL") && dataType.Length > 4 ? "BOOL" : dataType;
                hmiDataRows.Add(new string[] { varName, hmiBrand, hmiSymbol, hmiAddress, "", hmiDataType });
                successCount++;
            }
        }

        // 更新最终统计
        statsCallback?.Invoke(totalRows, successCount);

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
            log?.Invoke($"目标文件已成功写入：'{hmiFilepath}'");
            
            // 将错误行传递给调用者
            foreach (var err in errorRowsList)
            {
                errorCallback?.Invoke(err);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke($"写入目标文件时发生错误：{ex.Message}");
            return false;
        }
    }
}
