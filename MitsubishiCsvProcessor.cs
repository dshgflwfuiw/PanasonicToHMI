using System;
using System.Collections.Generic;

/// <summary>
/// 三菱PLC CSV处理器
/// 将三菱PLC变量文件转换为HMI格式
/// </summary>
public class MitsubishiCsvProcessor : PlcCsvProcessorBase
{
    protected override string BrandName => "Mitsubishi";
    
    protected override string AddressPattern => @"([A-Za-z]+)([0-9]+)";

    protected override Dictionary<string, string> PrefixMapping => new(StringComparer.OrdinalIgnoreCase)
    {
        { "X", "X" },      // 输入继电器
        { "Y", "Y" },      // 输出继电器
        { "M", "M" },      // 内部辅助继电器
        { "L", "L" },      // 锁存继电器
        { "S", "S" },      // 状态继电器
        { "T", "T" },      // 定时器
        { "C", "C" },      // 计数器
        { "SM", "SM" },    // 特殊辅助继电器
        { "D", "D" },      // 16位数据寄存器
        { "DD", "D" },     // 32位数据寄存器
        { "SD", "SD" },    // 16位特殊数据寄存器
        { "DSD", "SD" },   // 32位特殊数据寄存器
        { "Z", "Z" },      // 16位变址寄存器
        { "DZ", "Z" },     // 32位变址寄存器
        { "R", "R" },      // 16位锁存寄存器
        { "DR", "R" }      // 32位锁存寄存器
    };
}
