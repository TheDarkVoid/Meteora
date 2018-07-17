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
	static void Main(string[] args)
	{
		MeteoraWindow game = null; //Form
		var gameWindowCreate = new AutoResetEvent(false);
		var thread = new Thread(() =>
		{
			Application.EnableVisualStyles();
			game = new MeteoraWindow(new MeteoraTriangleView(), 1920, 1080);
			gameWindowCreate.Set();
			Application.Run(game);
			game.Dispose();
		});
		Console.Write("Creating window... ");
		thread.Start();
		gameWindowCreate.WaitOne();
		Console.WriteLine("Done!");
		Console.Write("Initializing... ");
		game.Init();
		Console.WriteLine("Done!");
		Console.WriteLine("Running Main Loop... ");
		game.DoMainLoop();
		thread.Join();
	}
}
