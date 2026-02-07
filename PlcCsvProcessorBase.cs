using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// PLC CSV处理器基类
/// 提供通用的编码检测、文件读写和转换逻辑
/// </summary>
public abstract class PlcCsvProcessorBase
{
    /// <summary>
    /// 品牌名称
    /// </summary>
    protected abstract string BrandName { get; }

    /// <summary>
    /// 前缀映射字典
    /// </summary>
    protected abstract Dictionary<string, string> PrefixMapping { get; }

    /// <summary>
    /// 解析PLC地址的正则表达式模式
    /// </summary>
    protected abstract string AddressPattern { get; }

    /// <summary>
    /// 尝试的编码列表
    /// </summary>
    protected virtual Encoding[] EncodingsToTry => new[]
    {
        Encoding.GetEncoding("GBK"),
        Encoding.GetEncoding("gb2312"),
        new UTF8Encoding(true),
        new UTF8Encoding(false),
        Encoding.GetEncoding("big5"),
        Encoding.GetEncoding("iso-8859-1")
    };

    /// <summary>
    /// 将PLC CSV文件转换为HMI格式
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

        var fileLines = TryReadFileWithEncoding(plcFilepath, log);
        if (fileLines == null)
        {
            log?.Invoke("无法找到合适的编码来正确读取源文件。");
            return false;
        }

        if (fileLines.Count > 1)
        {
            ProcessDataLines(fileLines, hmiDataRows, log);
        }

        if (hmiDataRows.Count == 1)
        {
            log?.Invoke("全部数据因格式不正确或列数不足被跳过，未生成输出文件。");
            return false;
        }

        return WriteOutputFile(hmiFilepath, hmiDataRows, log);
    }

    /// <summary>
    /// 尝试使用多种编码读取文件
    /// </summary>
    private List<string>? TryReadFileWithEncoding(string filepath, Action<string>? log)
    {
        foreach (var encoding in EncodingsToTry)
        {
            try
            {
                log?.Invoke($"尝试使用编码 '{encoding.WebName}' 打开源文件。");
                var tempLines = File.ReadAllLines(filepath, encoding);
                
                if (tempLines.Length > 0 && Regex.IsMatch(tempLines[0], @"[\u4e00-\u9fa5]"))
                {
                    log?.Invoke($"编码 '{encoding.WebName}' 验证成功：检测到中文字符。");
                    return new List<string>(tempLines);
                }
                else if (tempLines.Length <= 1)
                {
                    log?.Invoke($"文件为空或行数过少，假定编码 '{encoding.WebName}' 正确。");
                    return new List<string>(tempLines);
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

        return null;
    }

    /// <summary>
    /// 处理数据行
    /// </summary>
    private void ProcessDataLines(List<string> fileLines, List<string[]> hmiDataRows, Action<string>? log)
    {
        fileLines.RemoveAt(0);
        int rowCount = 0;
        
        foreach (var line in fileLines)
        {
            rowCount++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var row = ProcessSingleLine(line, rowCount, log);
            if (row != null)
            {
                hmiDataRows.Add(row);
            }
        }
    }

    /// <summary>
    /// 处理单行数据
    /// </summary>
    private string[]? ProcessSingleLine(string line, int rowCount, Action<string>? log)
    {
        string[] plcRow = line.Split(';');
        
        if (plcRow.Length < 6)
        {
            log?.Invoke($"跳过第 {rowCount} 行，因格式不正确或列数不足: {line}");
            return null;
        }

        string varName = plcRow[1].Trim();
        string plcAddr = plcRow[3].Trim();
        string dataType = plcRow[4].Trim();
        
        var (hmiSymbol, hmiAddress) = ParseAddress(plcAddr, varName, log);
        string hmiDataType = NormalizeDataType(dataType);
        
        return new[] { varName, BrandName, hmiSymbol, hmiAddress, "", hmiDataType };
    }

    /// <summary>
    /// 解析PLC地址
    /// </summary>
    protected virtual (string symbol, string address) ParseAddress(string plcAddr, string varName, Action<string>? log)
    {
        Match match = Regex.Match(plcAddr, AddressPattern);
        
        if (match.Success)
        {
            string originalPrefix = match.Groups[1].Value.ToUpper();
            string address = match.Groups[2].Value;
            
            string symbol = PrefixMapping.ContainsKey(originalPrefix) 
                ? PrefixMapping[originalPrefix] 
                : originalPrefix;
            
            return (symbol, address);
        }
        
        log?.Invoke($"无法解析变量 '{varName}' 的地址 '{plcAddr}'。");
        return ("UNKNOWN", plcAddr);
    }

    /// <summary>
    /// 标准化数据类型
    /// </summary>
    protected virtual string NormalizeDataType(string dataType)
    {
        return dataType.Contains("BOOL") && dataType.Length > 4 ? "BOOL" : dataType;
    }

    /// <summary>
    /// 写入输出文件
    /// </summary>
    private bool WriteOutputFile(string filepath, List<string[]> dataRows, Action<string>? log)
    {
        try
        {
            using (StreamWriter sw = new(filepath, false, new UTF8Encoding(true)))
            {
                foreach (var row in dataRows)
                    sw.WriteLine(string.Join(",", row.Select(s => $"\"{s}\"")));
            }
            log?.Invoke($"目标文件已成功写入: '{filepath}'");
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke($"写入目标文件时发生错误: {ex.Message}");
            return false;
        }
    }
}
