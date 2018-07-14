using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Meteora;
using System.Windows.Forms;
using System.Threading;
using Meteora.View;
using Vulkan;
using System.Runtime.InteropServices;
using System.IO;

class Program
{

	[DllImport("kernel32.dll", EntryPoint = "GetStdHandle", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
	public static extern IntPtr GetStdHandle(int nStdHandle);

	[DllImport("kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
	public static extern int AllocConsole();

	private const int STD_OUTPUT_HANDLE = -11;
	private const int MY_CODE_PAGE = 437;

	[STAThread]
	static void Main(string[] args)
	{
		Application.EnableVisualStyles();
		MeteoraWindow game = null;
		var thread = new Thread(() =>
		{
			game = new MeteoraWindow(new MeteoraTriangleView(), 1920, 1080);
			Application.Run(game);
			game.Dispose();
		});
		Console.Write("Creating window... ");
		thread.Start();
		while (true)
		{
			if (game != null)
				break;
		}
		Console.WriteLine("Done!");
		Console.Write("Initializing... ");
		game.Init();
		Console.WriteLine("Done!");
		Console.WriteLine("Running Main Loop... ");
		game.DoMainLoop();
		thread.Join();
	}
}
