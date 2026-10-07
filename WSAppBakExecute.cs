using System;
using System.Text;
using WSAppBak;

internal class WSAppBakExecute
{
	private static void Main(string[] args)
	{
		// 让控制台能够正确显示中文
		try
		{
			Console.OutputEncoding = Encoding.UTF8;
		}
		catch
		{
			// 个别输出被重定向的环境下设置编码可能失败，忽略即可
		}
		Console.Title = "WSAppBak - Windows 应用备份打包工具";
		WSAppBak.WSAppBak wSAppBak = new WSAppBak.WSAppBak();
		wSAppBak.Run();
	}
}
