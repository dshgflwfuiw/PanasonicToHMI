using System;
using System.Collections.Generic;

/// <summary>
/// 基恩士Keyence KV系列PLC CSV处理器
/// 将基恩士KV系列PLC变量文件转换为HMI格式
/// </summary>
public class KeyenceCsvProcessor : PlcCsvProcessorBase
{
    protected override string BrandName => "Keyence KV";

    protected override string AddressPattern => @"([A-Za-z]+)(\d+)";

    protected override Dictionary<string, string> PrefixMapping => new(StringComparer.OrdinalIgnoreCase)
    {
        { "X", "X" },      // 输入
        { "Y", "Y" },      // 输出
        { "R", "R" },      // 内部继电器
        { "M", "M" },      // 控制继电器
        { "LR", "LR" },    // 锁定继电器
        { "CR", "CR" },    // 控制继电器
        { "T", "T" },      // 定时器
        { "C", "C" },      // 计数器
        { "D", "D" },      // 数据存储器
        { "DM", "DM" },    // 数据存储器
        { "TN", "TN" },    // 定时器当前值
        { "CN", "CN" },    // 计数器当前值
    };
}
