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
    private char _detectedDelimiter = ';';

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
       Encoding.GetEncoding("gb18030"),
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
        var delimiterCandidates = new[] { ';', ',', '\t' };
        var headerKeywords = new[] { "名称", "变量", "地址", "Name", "Address" };

        foreach (var encoding in EncodingsToTry)
        {
            try
            {
                log?.Invoke($"尝试使用编码 '{encoding.WebName}' 打开源文件。");
                var tempLines = File.ReadAllLines(filepath, encoding);

                if (tempLines == null || tempLines.Length == 0)
                {
                    log?.Invoke($"编码 '{encoding.WebName}' 读取成功，但文件为空，继续尝试下一个编码...");
                    continue;
                }

                string headerLine = tempLines[0];
                char bestDelimiter = ';';
                int bestScore = -1;
                int bestValidRows = 0;
                int bestHeaderCount = 0;
                bool bestHasHeaders = false;

                foreach (var delimiter in delimiterCandidates)
                {
                    var headerParts = headerLine.Split(new[] { delimiter }, StringSplitOptions.None)
                        .Select(h => h.Trim())
                        .ToArray();

                    if (headerParts.Length < 2)
                    {
                        continue;
                    }

                    bool hasHeaders = headerParts.Any(p => headerKeywords.Any(k => p.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));
                    int validRows = tempLines.Skip(1).Count(line =>
                        !string.IsNullOrWhiteSpace(line) &&
                        line.Split(new[] { delimiter }, StringSplitOptions.None).Length >= 6);

                    int score = validRows + (hasHeaders ? 10 : 0) + headerParts.Length;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDelimiter = delimiter;
                        bestValidRows = validRows;
                        bestHeaderCount = headerParts.Length;
                        bestHasHeaders = hasHeaders;
                    }
                }

                if (bestScore >= 0 && (bestValidRows > 0 || bestHasHeaders || bestHeaderCount >= 6))
                {
                    _detectedDelimiter = bestDelimiter;
                    log?.Invoke($"编码 '{encoding.WebName}' 验证成功：分隔符='{bestDelimiter}'，表头列数={bestHeaderCount}，有效数据行={bestValidRows}。");
                    return new List<string>(tempLines);
                }

                if (tempLines.Length == 1)
                {
                    _detectedDelimiter = bestDelimiter;
                    log?.Invoke($"编码 '{encoding.WebName}' 读取成功且仅一行，接受该编码。");
                    return new List<string>(tempLines);
                }

                log?.Invoke($"编码 '{encoding.WebName}' 读取成功但行格式不符，继续尝试...");
            }
            catch (Exception ex)
            {
                log?.Invoke($"编码 '{encoding.WebName}' 解码失败：{ex.Message}，尝试下一个...");
            }
        }

        return null;
    }

    private static readonly string[] NameColumnCandidates =
    {
        "名称", "变量名称", "VARNAME", "VAR_NAME", "NAME", "变量"
    };

    private static readonly string[] AddressColumnCandidates =
    {
        "地址", "PLC地址", "映射地址", "PLC Address", "Address", "映射"
    };

    private static readonly string[] DataTypeColumnCandidates =
    {
        "数据类型", "类型", "Type", "DataType", "DType", "DTYPE"
    };

    private static char DetectDelimiter(string headerLine)
    {
        if (headerLine.Contains(';')) return ';';
        if (headerLine.Contains(',')) return ',';
        if (headerLine.Contains('\t')) return '\t';
        return ';';
    }

    /// <summary>
    /// 查找符合条件的列索引
    /// </summary>
    private static int FindColumnIndex(string[] columns, string[] candidates)
    {
        for (int i = 0; i < columns.Length; i++)
        {
            if (candidates.Any(candidate =>
                columns[i].Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return i;
            }
        }

        for (int i = 0; i < columns.Length; i++)
        {
            if (candidates.Any(candidate =>
                columns[i].IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
   /// 处理数据行
   /// </summary>
   protected virtual void ProcessDataLines(List<string> fileLines, List<string[]> hmiDataRows, Action<string>? log)
    {
        char delimiter = _detectedDelimiter;
        int headerIndex = FindHeaderIndex(fileLines, delimiter, out var headerColumns, log);

        if (headerIndex < 0 || headerColumns == null)
        {
            log?.Invoke("未能识别文件表头，使用默认列索引进行解析。");
            headerColumns = fileLines[0].Split(new[] { delimiter }, StringSplitOptions.None)
                .Select(c => c.Trim()).ToArray();
            headerIndex = 0;
        }

        int nameIndex = FindColumnIndex(headerColumns, NameColumnCandidates);
        int addressIndex = FindColumnIndex(headerColumns, AddressColumnCandidates);
        int dataTypeIndex = FindColumnIndex(headerColumns, DataTypeColumnCandidates);

        if (nameIndex < 0) nameIndex = 1;
        if (addressIndex < 0) addressIndex = 3;
        if (dataTypeIndex < 0) dataTypeIndex = 4;

        int rowCount = 0;
        for (int i = headerIndex + 1; i < fileLines.Count; i++)
        {
            rowCount++;
            var line = fileLines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (IsSectionHeaderLine(line, delimiter))
            {
                continue;
            }

            var row = ProcessSingleLine(line, rowCount, delimiter, nameIndex, addressIndex, dataTypeIndex, log);
            if (row != null)
            {
                hmiDataRows.Add(row);
            }
        }
    }

    private static bool IsSectionHeaderLine(string line, char delimiter)
    {
        var firstCell = line.Split(new[] { delimiter }, StringSplitOptions.None)[0].Trim();
        return firstCell.Equals("TYPENAME", StringComparison.OrdinalIgnoreCase)
               || firstCell.Equals("VARNUMBER", StringComparison.OrdinalIgnoreCase)
               || firstCell.Equals("SECTION", StringComparison.OrdinalIgnoreCase)
               || firstCell.Equals("ST_TYPE", StringComparison.OrdinalIgnoreCase);
    }

    private static int FindHeaderIndex(List<string> fileLines, char delimiter, out string[]? headerColumns, Action<string>? log)
    {
        headerColumns = null;
        int maxScan = Math.Min(fileLines.Count, 20);

        for (int i = 0; i < maxScan; i++)
        {
            var line = fileLines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var columns = line.Split(new[] { delimiter }, StringSplitOptions.None)
                .Select(c => c.Trim()).ToArray();

            int nameIndex = FindColumnIndex(columns, NameColumnCandidates);
            int addressIndex = FindColumnIndex(columns, AddressColumnCandidates);
            if (nameIndex >= 0 && addressIndex >= 0)
            {
                headerColumns = columns;
                if (i > 0)
                {
                    log?.Invoke($"在第 {i + 1} 行找到表头，列索引: 名称={nameIndex}, 地址={addressIndex}。");
                }
                return i;
            }
        }

        return -1;
    }

    /// <summary>
   /// 处理单行数据
   /// </summary>
   protected virtual string[]? ProcessSingleLine(string line, int rowCount, char delimiter, int nameIndex, int addressIndex, int dataTypeIndex, Action<string>? log)
    {
        string[] plcRow = line.Split(new[] { delimiter }, StringSplitOptions.None);

        if (plcRow.Length <= Math.Max(nameIndex, Math.Max(addressIndex, dataTypeIndex)))
        {
            log?.Invoke($"跳过第 {rowCount} 行，因格式不正确或列数不足: {line}");
            return null;
        }

        string varName = plcRow[nameIndex].Trim();
        string plcAddr = plcRow[addressIndex].Trim();
        string dataType = plcRow[dataTypeIndex].Trim();

        if (string.IsNullOrWhiteSpace(plcAddr))
        {
            return null;
        }

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
   protected virtual bool WriteOutputFile(string filepath, List<string[]> dataRows, Action<string>? log)
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
