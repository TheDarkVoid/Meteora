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

class Program
{
	[STAThread]
	static void Main(string[] args)
	{
		Application.EnableVisualStyles();
		using (MeteoraWindow gameWindow = new MeteoraWindow(new MeteoraTriangleView(), 1920, 1080))
		{
			Application.Run(gameWindow);
		}
	}
}
