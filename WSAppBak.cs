using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;

namespace WSAppBak
{
	internal class WSAppBak
	{
		private string AppName = "Windows 应用商店应用备份";

		private string AppCreator = "Kiran Murmu";

		private string AppCurrentDirctory = Directory.GetCurrentDirectory();

		private string WSAppXmlFile = "AppxManifest.xml";

		private bool Checking = true;

		private string WSAppName;

		private string WSAppPath;

		private string WSAppVersion;

		private string WSAppFileName;

		private string WSAppOutputPath;

		private string WSAppProcessorArchitecture;

		private string WSAppPublisher;

		// 生成的签名证书 PFX 固定使用此密码（仅用于内部签名，用户无需关心）
		private string PfxPassword = "wsappbak";

		public void Run()
		{
			ReadArg();
		}

		private string RunProcess(string fileName, string args)
		{
			string result = "";
			Process process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = fileName,
					Arguments = args,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true
				}
			};
			process.Start();
			while (!process.StandardOutput.EndOfStream)
			{
				string text = process.StandardOutput.ReadLine();
				Console.WriteLine(text);
				if (text.Length > 0)
				{
					result = text;
				}
			}
			return result;
		}

		// 运行一段 PowerShell 脚本（以 EncodedCommand 传入，彻底避免引号转义问题），成功返回 true
		private bool RunPowerShell(string script)
		{
			string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
			Process process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "powershell.exe",
					Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				}
			};
			process.Start();
			string stdout = process.StandardOutput.ReadToEnd();
			string stderr = process.StandardError.ReadToEnd();
			process.WaitForExit();
			if (stdout.Length > 0)
			{
				Console.Write(stdout);
			}
			if (stderr.Length > 0)
			{
				Console.WriteLine(stderr);
			}
			return process.ExitCode == 0;
		}

		// 把字符串包装成 PowerShell 单引号字符串
		private string PSQuote(string s)
		{
			return "'" + (s ?? "").Replace("'", "''") + "'";
		}

		private void ReadArg()
		{
			while (Checking)
			{
				Console.Clear();
				Console.WriteLine("\t\t“{0}”  作者：{1}", AppName, AppCreator);
				Console.WriteLine("================================================================================");
				Console.Write("请输入应用路径：");
				WSAppPath = Convert.ToString(Console.ReadLine());
				if (WSAppPath.Contains("\""))
				{
					WSAppPath = WSAppPath.Replace("\"", "");
					WSAppPath = "\"" + WSAppPath + "\"";
				}
				else if (File.Exists(WSAppPath + "\\" + WSAppXmlFile))
				{
					while (Checking)
					{
						Console.Write("\n请输入输出路径：");
						WSAppOutputPath = Convert.ToString(Console.ReadLine());
						if (WSAppOutputPath.Contains("\""))
						{
							WSAppOutputPath = WSAppOutputPath.Replace("\"", "");
							WSAppOutputPath = "\"" + WSAppOutputPath + "\"";
						}
						else if (Directory.Exists(WSAppOutputPath))
						{
							WSAppFileName = Path.GetFileName(WSAppPath);
							using (XmlReader xmlReader = XmlReader.Create(WSAppPath + "\\" + WSAppXmlFile))
							{
								while (xmlReader.Read())
								{
									if (xmlReader.IsStartElement() && xmlReader.Name == "Identity")
									{
										string text = xmlReader["Name"];
										if (text != null)
										{
											WSAppName = text;
										}
										string text2 = xmlReader["Publisher"];
										if (text2 != null)
										{
											WSAppPublisher = text2;
										}
										string text3 = xmlReader["Version"];
										if (text3 != null)
										{
											WSAppVersion = text3;
										}
										string text4 = xmlReader["ProcessorArchitecture"];
										if (text4 != null)
										{
											WSAppProcessorArchitecture = text4;
										}
									}
								}
							}
							while (Checking)
							{
								MakeAppx();
							}
						}
						else
						{
							Checking = true;
							Console.WriteLine("\n无效的输出路径，目录“{0}”不存在！", WSAppOutputPath);
							Console.Write("按任意键重试……");
							Console.ReadKey();
							Console.Clear();
							Console.WriteLine("\t\t“{0}”  作者：{1}", AppName, AppCreator);
							Console.Write("================================================================================");
						}
					}
				}
				else
				{
					Checking = true;
					Console.WriteLine("\n无效的应用路径，未找到“{0}”文件！", WSAppXmlFile);
					Console.Write("按任意键重试……");
					Console.ReadKey();
				}
			}
		}

		private void MakeAppx()
		{
			string text = AppCurrentDirctory + "\\WSAppBak\\MakeAppx.exe";
			string args = "pack -d \"" + WSAppPath + "\" -p \"" + WSAppOutputPath + "\\" + WSAppFileName + ".appx\" -l";
			if (File.Exists(text))
			{
				if (File.Exists(WSAppOutputPath + "\\" + WSAppFileName + ".appx"))
				{
					File.Delete(WSAppOutputPath + "\\" + WSAppFileName + ".appx");
				}
				Console.WriteLine("\n请稍候……正在创建“.appx”程序包文件。\n");
				if (RunProcess(text, args).ToLower().Contains("succeeded"))
				{
					Console.Clear();
					Console.WriteLine("\t\t“{0}”  作者：{1}", AppName, AppCreator);
					Console.WriteLine("================================================================================");
					Console.WriteLine("程序包“{0}”创建成功。", WSAppFileName + ".appx");
					while (Checking)
					{
						CreateCertificate();
					}
				}
				else
				{
					Checking = false;
					Console.Clear();
					Console.WriteLine("\t\t“{0}”  作者：{1}", AppName, AppCreator);
					Console.WriteLine("================================================================================");
					Console.Write("程序包“{0}”创建失败……按任意键退出。", WSAppFileName + ".appx");
					Console.ReadKey();
				}
			}
			else
			{
				Checking = false;
				Console.WriteLine("\n无法创建“.appx”文件，未找到“MakeAppx.exe”！");
				Console.Write("按任意键退出……");
				Console.ReadKey();
			}
		}

		// 使用 Windows 自带的 PowerShell 生成代码签名证书，直接导出 .cer 与 .pfx；
		// 不再调用会弹出英文“Create Private Key Password”窗口的 MakeCert.exe / Pvk2Pfx.exe
		private void CreateCertificate()
		{
			string cerPath = WSAppOutputPath + "\\" + WSAppFileName + ".cer";
			string pfxPath = WSAppOutputPath + "\\" + WSAppFileName + ".pfx";
			if (File.Exists(cerPath))
			{
				File.Delete(cerPath);
			}
			if (File.Exists(pfxPath))
			{
				File.Delete(pfxPath);
			}

			Console.WriteLine("\n请稍候……正在为程序包创建证书。\n");
			Console.Write("证书创建：");

			string script =
				"$ErrorActionPreference='Stop';" +
				"$c = New-SelfSignedCertificate -Type CodeSigningCert -Subject " + PSQuote(WSAppPublisher) +
				" -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256" +
				" -NotAfter (Get-Date).AddYears(10) -CertStoreLocation Cert:\\CurrentUser\\My;" +
				"Export-Certificate -Cert $c -FilePath " + PSQuote(cerPath) + " | Out-Null;" +
				"$pw = ConvertTo-SecureString -String " + PSQuote(PfxPassword) + " -Force -AsPlainText;" +
				"Export-PfxCertificate -Cert $c -FilePath " + PSQuote(pfxPath) + " -Password $pw | Out-Null;" +
				"Remove-Item ('Cert:\\CurrentUser\\My\\' + $c.Thumbprint) -Force;";

			if (RunPowerShell(script))
			{
				Console.Write("成功");
				while (Checking)
				{
					SignApp();
				}
			}
			else
			{
				Checking = false;
				Console.WriteLine("\n无法为程序包创建证书……按任意键退出。");
				Console.ReadKey();
			}
		}

		private void SignApp()
		{
			string text = AppCurrentDirctory + "\\WSAppBak\\SignTool.exe";
			string args = "sign -fd SHA256 -f \"" + WSAppOutputPath + "\\" + WSAppFileName + ".pfx\" -p " + PfxPassword + " \"" + WSAppOutputPath + "\\" + WSAppFileName + ".appx\"";
			if (File.Exists(text))
			{
				Console.WriteLine("\n\n请稍候……正在对程序包进行签名，这可能需要几分钟。\n");
				if (RunProcess(text, args).ToLower().Contains("successfully signed"))
				{
					Checking = false;
					Console.WriteLine("程序包签名成功。安装应用程序包之前，请先将“.cer”文件安装到 [本地计算机\\受信任的根证书颁发机构]；或者使用“WSAppPkgIns.exe”来安装应用程序包！");
					Console.Write("\n按任意键退出……  :)");
					Console.ReadKey();
				}
				else
				{
					Checking = false;
					Console.WriteLine("\n无法对程序包进行签名，按任意键退出……");
					Console.ReadKey();
				}
			}
			else
			{
				Checking = false;
				Console.WriteLine("\n无法对程序包进行签名，未找到“SignTool.exe”！");
				Console.Write("按任意键退出……");
				Console.ReadKey();
			}
		}
	}
}
