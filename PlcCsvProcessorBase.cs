using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// PLC CSV 处理器基类（抽象）
/// 
/// 该类封装了从 PLC 导出的 CSV 文件转换为目标 HMI CSV 的通用流程：
/// 1. 尝试使用多种编码打开源文件并检测最佳分隔符；
/// 2. 定位表头行并识别“名称/地址/数据类型”列索引；
/// 3. 按行解析每一条变量记录（交由 <see cref="ProcessSingleLine"/> 处理）；
/// 4. 将解析后的 HMI 格式行写入输出文件。
/// 
/// 扩展点：
/// - 继承该类并实现抽象属性：<see cref="BrandName"/>、<see cref="PrefixMapping"/>、<see cref="AddressPattern"/>。
/// - 如需品牌特有的地址解析规则，可重写 <see cref="ParseAddress(string,string,Action{string}?)"/>。
/// - 如需品牌特有的行级别过滤或预处理，可重写 <see cref="ShouldSkipAddress(string,string,Action{string}?)"/> 或 <see cref="ProcessSingleLine"/>。
/// 
/// 设计目标：最大化代码复用、在大多数 PLC 品牌之间共享解析逻辑，并提供易于定制的钩子。
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
    /// 尝试打开源文件时使用的编码优先列表。
    /// 子类可以覆盖此属性以改变尝试顺序或添加/移除编码。
    /// 通常包含常见的中文编码以及 UTF-8/Big5/ISO-8859-1 等。
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
    /// <summary>
    /// 将一个 PLC CSV 文件解析并写入目标 HMI CSV 文件。
    /// 
    /// 参数:
    /// - <paramref name="plcFilepath"/>: 源 PLC CSV 文件路径。
    /// - <paramref name="hmiFilepath"/>: 输出 HMI CSV 文件路径。
    /// - <paramref name="log"/>: 可选的日志回调，用于输出解析过程中的信息/警告/错误。
    /// 
    /// 返回值: 成功写入返回 true，否则返回 false。
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
    /// 尝试使用多个编码读取源文件并返回按行分割的文本内容。
    /// 
    /// 实现要点：
    /// - 对每种编码，使用可共享读取（FileShare.ReadWrite）打开文件，避免被其他应用（如 Excel）占用时直接失败；
    /// - 如果文件被占用，会做有限次数的重试；
    /// - 同时对常见分隔符（; , \t）做简单的表头与有效行统计，以选出最可能的分隔符。
    /// 
    /// 返回：成功时返回按行的字符串列表；失败时返回 null。
    /// </summary>
    protected List<string>? TryReadFileWithEncoding(string filepath, Action<string>? log)
    {
        var delimiterCandidates = new[] { ';', ',', '\t' };
        var headerKeywords = new[] { "名称", "变量", "地址", "Name", "Address" };

        foreach (var encoding in EncodingsToTry)
        {
                try
                {
                    log?.Invoke($"尝试使用编码 '{encoding.WebName}' 打开源文件。");

                    // 尝试以允许共享的方式打开文件，并在被占用时重试几次。
                    string[] tempLines = null!;
                    const int maxAttempts = 5;
                    for (int attempt = 1; attempt <= maxAttempts; attempt++)
                    {
                        try
                        {
                            using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var sr = new StreamReader(fs, encoding))
                            {
                                // 读取所有行到临时列表
                                var lines = new List<string>();
                                string? ln;
                                while ((ln = sr.ReadLine()) != null)
                                    lines.Add(ln);
                                tempLines = lines.ToArray();
                            }
                            break;
                        }
                        catch (IOException ioEx) when (attempt < maxAttempts)
                        {
                            log?.Invoke($"文件正在被另一个进程使用，等待重试 (第 {attempt} 次): {ioEx.Message}");
                            Thread.Sleep(200);
                            continue;
                        }
                    }

                    if (tempLines == null || tempLines.Length == 0)
                    {
                        log?.Invoke($"编码 '{encoding.WebName}' 读取成功，但文件为空或无法读取，继续尝试下一个编码...");
                        continue;
                    }
                // 读取成功，尝试检测分隔符和表头
                string headerLine = tempLines[0];
                char bestDelimiter = ';';
                int bestScore = -1;
                int bestValidRows = 0;
                int bestHeaderCount = 0;
                bool bestHasHeaders = false;

                foreach (var delimiter in delimiterCandidates)
                {
                    // 计算当前分隔符的得分
                    var headerParts = headerLine.Split(new[] { delimiter }, StringSplitOptions.None)
                        .Select(h => h.Trim())
                        .ToArray();

                    if (headerParts.Length < 2)
                    {
                        continue;
                    }
                    // 检查表头中是否包含关键字
                    bool hasHeaders = headerParts.Any(p => headerKeywords.Any(k => p.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));
                    int validRows = tempLines.Skip(1).Count(line =>
                        !string.IsNullOrWhiteSpace(line) &&
                        line.Split(new[] { delimiter }, StringSplitOptions.None).Length >= 6);
                    // 计算得分
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

    /// <summary>
    /// 解析 PLC CSV 文件为 HMI 行（不写入文件）
    /// 返回解析出的数据行（不包含表头）。解析失败返回 null。
    /// </summary>
    public List<string[]>? ParsePlcCsvToHmiRows(string plcFilepath, Action<string>? log)
    {
        if (!File.Exists(plcFilepath))
        {
            log?.Invoke($"文件 '{plcFilepath}' 不存在，跳过。");
            return null;
        }

        var fileLines = TryReadFileWithEncoding(plcFilepath, log);
        if (fileLines == null)
        {
            log?.Invoke($"无法以已知编码读取文件: {plcFilepath}");
            return null;
        }

        var hmiDataRows = new List<string[]>();
        if (fileLines.Count > 1)
        {
            ProcessDataLines(fileLines, hmiDataRows, log);
        }

        return hmiDataRows;
    }

    /// <summary>
    /// 判断是否应跳过该地址（范围地址或列表地址等）
    /// 子类可重写以实现品牌特有的过滤逻辑
    /// </summary>
    /// <summary>
    /// 判定是否跳过某个地址的默认实现。
    /// 
    /// 默认行为：
    /// - 如果地址中包含方括号 `[` 或 `]`（表示数组/范围），或包含逗号（表示列表），则跳过；
    /// - 如果地址字符串包含关键字 `ARRAY`（不区分大小写），则跳过。
    /// 
    /// 子类可以重写此方法实现更复杂或更宽松的策略。
    /// </summary>
    protected virtual bool ShouldSkipAddress(string plcAddr, string varName, Action<string>? log)
    {
        if (plcAddr.IndexOf('[') >= 0 || plcAddr.IndexOf(']') >= 0 || plcAddr.IndexOf(',') >= 0)
        {
            log?.Invoke($"跳过变量 '{varName}' 的地址 '{plcAddr}'：范围或列表地址不支持。");
            return true;
        }

        // 过滤包含关键字 ARRAY 的地址（不区分大小写）
        if (plcAddr.IndexOf("ARRAY", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            log?.Invoke($"跳过变量 '{varName}' 的地址 '{plcAddr}'：包含不支持的关键字 'ARRAY'。");
            return true;
        }
        return false;
    }

    /// <summary>
    /// 表头中可能表示变量名称的候选列名列表（按优先级/常见性排列）。
    /// 用于在表头中匹配并确定名称列的索引。
    /// </summary>
    private static readonly string[] NameColumnCandidates =
    {
        "名称", "变量名称", "VARNAME", "VAR_NAME", "NAME", "变量"
    };

    /// <summary>
    /// 表头中可能表示地址的候选列名列表。
    /// 用于在表头中识别地址列的索引。
    /// </summary>
    private static readonly string[] AddressColumnCandidates =
    {
        "地址", "PLC地址", "映射地址", "PLC Address", "Address", "映射"
    };

    /// <summary>
    /// 表头中可能表示数据类型的候选列名列表。
    /// 用于在表头中识别数据类型列的索引。
    /// </summary>
    private static readonly string[] DataTypeColumnCandidates =
    {
        "数据类型", "类型", "Type", "DataType", "DType", "DTYPE"
    };

    /// <summary>
    /// 从表头行中快速检测最可能的分隔符（';'、',' 或制表符）。
    /// 这是一个轻量优先检查方法，基于字符存在性判断分隔符，作为备选策略。
    /// </summary>
    private static char DetectDelimiter(string headerLine)
    {
        if (headerLine.Contains(';')) return ';';
        if (headerLine.Contains(',')) return ',';
        if (headerLine.Contains('\t')) return '\t';
        return ';';
    }

    /// <summary>
    /// 在给定的列名数组中查找第一个匹配候选名称的列索引。
    /// 
    /// 匹配策略：
    /// 1. 精确匹配（忽略大小写）；
    /// 2. 如果没有精确匹配，则尝试子串包含匹配（忽略大小写）。
    /// 
    /// 返回匹配到的列索引，未匹配时返回 -1。
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
    /// 处理文件中所有数据行并将解析结果追加到 <paramref name="hmiDataRows"/> 中。
    /// 
    /// 该方法负责：
    /// - 根据已检测的分隔符找到表头并识别名称/地址/数据类型列索引；
    /// - 遍历每一行并调用 <see cref="ProcessSingleLine"/> 处理单行；
    /// - 将非空解析结果追加到目标集合中。
    /// 
    /// 子类通常不需要重写此方法，除非需要完全替换行迭代逻辑。
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

    /// <summary>
    /// 检查一行是否为节（section）头或非数据行，通常出现在 CSV 文件的某些格式中。
    /// 如果第一列匹配常见的节头关键字（如 TYPENAME、VARNUMBER、SECTION、ST_TYPE）则视为节头并跳过。
    /// </summary>
    private static bool IsSectionHeaderLine(string line, char delimiter)
    {
        var firstCell = line.Split(new[] { delimiter }, StringSplitOptions.None)[0].Trim();
        return firstCell.Equals("TYPENAME", StringComparison.OrdinalIgnoreCase)
               || firstCell.Equals("VARNUMBER", StringComparison.OrdinalIgnoreCase)
               || firstCell.Equals("SECTION", StringComparison.OrdinalIgnoreCase)
               || firstCell.Equals("ST_TYPE", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 在文件前若干行中查找表头（名称和地址列同时存在的行）。
    /// 
    /// 返回值：找到时返回表头所在的行索引（0 基）；否则返回 -1。
    /// 输出参数 <paramref name="headerColumns"/> 为表头拆分后的列名数组（找到时非空）。
    /// </summary>
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
    /// 处理单行 CSV 数据并返回 HMI 行数组（长度固定为 6 列）。
    /// 
    /// 默认实现：
    /// - 按分隔符切分行；
    /// - 根据列索引提取变量名、PLC 地址和数据类型；
    /// - 调用 <see cref="ShouldSkipAddress"/> 判断是否跳过该地址；
    /// - 调用 <see cref="ParseAddress"/> 将 PLC 地址解析为 HMI 符号与地址；
    /// - 调用 <see cref="NormalizeDataType"/> 标准化数据类型；
    /// - 返回格式化的字符串数组：{ 名称, 品牌, 起始符号, 起始地址, "", 数据类型 }。
    /// 
    /// 子类可重写此方法以实现品牌特有的行级解析或预处理逻辑。
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
        //空地址直接跳过
        if (string.IsNullOrWhiteSpace(plcAddr))
        {
            return null;
        }

        // 使用基类通用的地址跳过策略（子类可重写 ShouldSkipAddress）
        if (ShouldSkipAddress(plcAddr, varName, log))
        {
            return null;
        }

        // 通用的数据类型跳过规则：包含 ARRAY 的数据类型默认为数组，跳过
        if (!string.IsNullOrEmpty(dataType) && dataType.IndexOf("ARRAY", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            log?.Invoke($"跳过第 {rowCount} 行，数据类型包含不支持的关键字 'ARRAY': {dataType}");
            return null;
        }

        var (hmiSymbol, hmiAddress) = ParseAddress(plcAddr, varName, log);
        string hmiDataType = NormalizeDataType(dataType);

        return new[] { varName, BrandName, hmiSymbol, hmiAddress, "", hmiDataType };
    }

    /// <summary>
    /// 解析 PLC 地址为 HMI 符号与起始地址。
    /// 
    /// 默认实现使用抽象属性 <see cref="AddressPattern"/> 作为正则表达式进行匹配，并通过
    /// <see cref="PrefixMapping"/> 将匹配到的前缀转换为目标 HMI 符号。
    /// 
    /// 返回元组：(symbol, address)。解析失败应返回 ("UNKNOWN", plcAddr) 并可使用 <paramref name="log"/> 记录。
    /// 子类可重写以支持更复杂的地址格式或前缀规则。
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
    /// 标准化数据类型字符串，例如把包含 "BOOL" 的复杂类型统一转换为 "BOOL"。
    /// 子类可以覆盖以实现更复杂的类型映射。
    /// </summary>
    protected virtual string NormalizeDataType(string dataType)
    {
        return dataType.Contains("BOOL") && dataType.Length > 4 ? "BOOL" : dataType;
    }

    /// <summary>
    /// 将解析好的 HMI 数据行写入目标 CSV 文件。
    /// 
    /// 默认实现使用 UTF-8 带 BOM（便于 Windows 系统上的 Excel 正确识别）并将每个单元格用双引号包裹。
    /// 返回 true 表示写入成功，false 表示发生错误并通过 <paramref name="log"/> 记录异常信息。
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
