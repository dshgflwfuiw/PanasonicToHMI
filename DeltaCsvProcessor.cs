using System;
using System.Collections.Generic;

/// <summary>
/// 台达Delta DVP系列PLC CSV处理器
/// 将台达DVP系列PLC变量文件转换为HMI格式
/// </summary>
public class DeltaCsvProcessor : PlcCsvProcessorBase
{
    protected override string BrandName => "Delta DVP";

    protected override string AddressPattern => @"([A-Za-z]+)(\d+)";

    protected override Dictionary<string, string> PrefixMapping => new(StringComparer.OrdinalIgnoreCase)
    {
        { "X", "X" },      // 输入继电器
        { "Y", "Y" },      // 输出继电器
        { "M", "M" },      // 内部继电器
        { "S", "S" },      // 步进
        { "T", "T" },      // 定时器
        { "C", "C" },      // 计数器
        { "D", "D" },      // 16位数据寄存器
        { "DD", "D" },     // 32位数据寄存器
    };
}
