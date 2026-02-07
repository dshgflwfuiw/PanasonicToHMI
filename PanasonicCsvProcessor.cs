using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// 松下PLC CSV处理器
/// 将松下FP/KW系列PLC变量文件转换为HMI格式
/// </summary>
public class PanasonicCsvProcessor : PlcCsvProcessorBase
{
    protected override string BrandName => "Panasonic FP/KW";
    
    protected override string AddressPattern => @"([A-Za-z]+)([0-9A-Fa-f]+)";

    protected override Dictionary<string, string> PrefixMapping => new(StringComparer.OrdinalIgnoreCase)
    {
        { "WR", "WR" },   // 16位数据寄存器
        { "DWR", "WR" },  // 32位数据寄存器
        { "SV", "SV" },   // 16位数据保持寄存器
        { "DSV", "SV" },  // 32位数据保持寄存器
        { "EV", "EV" },   // 16位数据事件寄存器
        { "DEV", "EV" },  // 32位数据事件寄存器
        { "LD", "LD" },   // 16位数据链接寄存器
        { "DLD", "LD" },  // 32位数据链接寄存器
        { "WX", "WX" },   // 16位数据输入字
        { "DWX", "WX" },  // 32位数据输入字
        { "WY", "WY" },   // 16位数据输出字
        { "DWY", "WY" },  // 32位数据输出字
        { "WL", "WL" },   // 16位数据链接字
        { "DWL", "WL" },  // 32位数据链接字
        { "FL", "FL" },   // 16位数据标志位
        { "DFL", "FL" },  // 32位数据标志位
        { "DT", "DT" },   // 16位数据定时器
        { "DDT", "DT" }   // 32位数据定时器
    };
}
