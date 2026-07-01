using System;
using System.Collections.Generic;

/// <summary>
/// 汇川Inovance H3U/H5U系列PLC CSV处理器
/// 将汇川H3U/H5U系列PLC变量文件转换为HMI格式
/// </summary>
public class HuichuanCsvProcessor : PlcCsvProcessorBase
{
    protected override string BrandName => "Inovance H3U/H5U";

    protected override string AddressPattern => @"([A-Za-z]+)(\d+)";

    protected override Dictionary<string, string> PrefixMapping => new(StringComparer.OrdinalIgnoreCase)
    {
        { "X", "X" },      // 输入
        { "Y", "Y" },      // 输出
        { "M", "M" },      // 内部辅助继电器
        { "S", "S" },      // 状态
        { "T", "T" },      // 定时器
        { "C", "C" },      // 计数器
        { "D", "D" },      // 16位数据寄存器
        { "DD", "D" },     // 32位数据寄存器
        { "SM", "SM" },    // 特殊辅助继电器
        { "SD", "SD" },    // 16位特殊寄存器
        { "DSD", "SD" },   // 32位特殊寄存器
        { "Z", "Z" },      // 16位变址寄存器
        { "DZ", "Z" },     // 32位变址寄存器
        { "R", "R" },      // 锁存寄存器
        { "DR", "R" },     // 32位锁存寄存器
    };
}
