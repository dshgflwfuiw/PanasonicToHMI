namespace PanasonicToHmi.Tests;

public class TestData
{
    public static string GetSampleCsv()
    {
        return @"Variable,Address,Comment
StartButton,X0,启动按钮
StopButton,X1,停止按钮
MotorRun,Y0,电机运行
FaultAlarm,Y1,故障报警
SpeedSet,D100,速度设定值
CurrentVal,D200,当前值";
    }

    public static string GetInvalidCsv()
    {
        return @"Variable,Address,Comment
ValidVar,X0,有效变量
InvalidNoPrefix,123,无效地址无前缀
AnotherValid,Y1,另一个有效
BadFormat,,空地址";
    }
}
