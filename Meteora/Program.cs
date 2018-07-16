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
	private static MeteoraWindow game = null; //Form
	private static object gLock = new object();
	static void Main(string[] args)
	{
		var thread = new Thread(() =>
		{
			lock(gLock)
			{
				Application.EnableVisualStyles();
				game = new MeteoraWindow(new MeteoraTriangleView(), 1920, 1080);
			}
			Application.Run(game);
			game.Dispose();
		});
		Console.Write("Creating window... ");
		thread.Start();
		while (true)
		{
			lock(gLock)
			{
				if (game != null)
					break;
			}
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
