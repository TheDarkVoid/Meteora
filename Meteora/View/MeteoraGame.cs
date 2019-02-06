using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Meteora.Data;
using Vulkan;

namespace Meteora.View
{
	public class MeteoraGame
	{
		

		public MeteoraWindow Window { get; private set; }
		public MeteoraGame(GameCreateInfo createInfo = null)
		{
			if(createInfo == null)
			{
				createInfo = new GameCreateInfo
				{
					Height = 720,
					Width = 1280,
					View = new Meteora3DView()
				};
			}
			Window = new MeteoraWindow(createInfo);
		}

		public void Start()
		{
			Console.Write("Initializing... ");
			Window.Init();
			Console.WriteLine("Done!");
			Console.WriteLine("Running Main Loop... ");
			Window.RenderLoop();
			Window.Dispose();
		}

		

	}
}
