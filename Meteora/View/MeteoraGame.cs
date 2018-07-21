using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Meteora.Data;

namespace Meteora.View
{
	public class MeteoraGame
	{
		public MeteoraWindow Window { get; private set; }
		private AutoResetEvent gameWindowCreate;
		private Thread thread;
		public MeteoraGame(GameCreateInfo createInfo = null)
		{
			if(createInfo == null)
			{
				createInfo = new GameCreateInfo
				{
					Height = 720,
					Width = 1280,
					View = new MeteoraTriangleView()
				};
			}
			gameWindowCreate = new AutoResetEvent(false);
			thread = new Thread(() =>
			{
				Application.EnableVisualStyles();
				Window = new MeteoraWindow(createInfo);
				gameWindowCreate.Set();
				Application.Run(Window);
			});
			thread.Start();
		}

		public void Start()
		{
			gameWindowCreate.WaitOne();
			Console.Write("Initializing... ");
			Window.Init();
			Console.WriteLine("Done!");
			Console.WriteLine("Running Main Loop... ");
			Window.DoMainLoop();
			thread.Join();
		}

	}
}
