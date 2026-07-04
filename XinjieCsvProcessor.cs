using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// 信捷PLC CSV处理器
/// 将信捷PLC变量文件转换为HMI格式
/// </summary>
public class XinjieCsvProcessor : PlcCsvProcessorBase
{
    protected override string BrandName => "XINJE XD Series";

    protected override string AddressPattern => @"([A-Za-z]+)(\d+)";

    protected override Dictionary<string, string> PrefixMapping => new(StringComparer.OrdinalIgnoreCase)
    {
        { "M", "M" },
        { "D", "D" },
        { "X", "X" },
        { "Y", "Y" },
        { "C", "C" },
        { "CD", "CD" },
        { "T", "T" },
        { "TD", "TD" },
        { "HM", "HM" },
        { "HD", "HD" },
        { "HCD", "HCD" },
        { "HTD", "HTD" },
        { "X_Extension", "X_Extension" },
        { "Y_Extension", "Y_Extension" },
    };

    /// <summary>
    /// 处理单行数据（增加范围地址过滤）
    /// </summary>
    protected override string[]? ProcessSingleLine(string line, int rowCount, char delimiter, int nameIndex, int addressIndex, int dataTypeIndex, Action<string>? log)
    {
        string[] plcRow = line.Split(new[] { delimiter }, StringSplitOptions.None);

        if (plcRow.Length <= Math.Max(nameIndex, Math.Max(addressIndex, dataTypeIndex)))
        {
            log?.Invoke($"跳过第 {rowCount} 行，因格式不正确或列数不足: {line}");
            return null;
        }

        string varName = plcRow[nameIndex].Trim();
        string plcAddr = plcRow[addressIndex].Trim();

        if (string.IsNullOrWhiteSpace(plcAddr))
        {
            return null;
        }

        // 使用基类的地址跳过策略（包含 [] 、, 以及关键词 ARRAY）
        if (ShouldSkipAddress(plcAddr, varName, log))
        {
            return null;
        }

        string dataType = plcRow[dataTypeIndex].Trim();

        // 如果数据类型包含 ARRAY，则视为不支持的数组类型，跳过该行
        if (!string.IsNullOrEmpty(dataType) && dataType.IndexOf("ARRAY", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            log?.Invoke($"跳过第 {rowCount} 行，数据类型包含不支持的关键字 'ARRAY': {dataType}");
            return null;
        }
        var (hmiSymbol, hmiAddress) = ParseAddress(plcAddr, varName, log);

        if (hmiSymbol == "UNKNOWN" || string.IsNullOrEmpty(hmiSymbol))
        {
            log?.Invoke($"跳过第 {rowCount} 行，不支持的地址格式: {plcAddr}");
            return null;
        }

        string hmiDataType = NormalizeDataType(dataType);
        return new[] { varName, BrandName, hmiSymbol, hmiAddress, "", hmiDataType };
    }

    /// <summary>
    /// 解析PLC地址（支持 H 前缀变体和扩展逻辑）
    /// </summary>
    protected override (string symbol, string address) ParseAddress(string plcAddr, string varName, Action<string>? log)
    {
        var match = Regex.Match(plcAddr, @"^([MDXY]|H[MDXY])(\d+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            log?.Invoke($"无法解析变量 '{varName}' 的地址 '{plcAddr}'。");
            return ("UNKNOWN", plcAddr);
        }

        var prefix = match.Groups[1].Value.ToUpperInvariant();
        var numberText = match.Groups[2].Value;
        var number = int.Parse(numberText);

        if (prefix == "X"  && number >= 10000)
        {
            prefix = "X_Extension";
        }
        else if (prefix == "Y"  && number >= 10000)
        {
            prefix = "Y_Extension";
        }

        string symbol = PrefixMapping.ContainsKey(prefix) ? PrefixMapping[prefix] : prefix;
        return (symbol, numberText);
    }
}
