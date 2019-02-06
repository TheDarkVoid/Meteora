using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Meteora.View;
using SDL2;

namespace Meteora.Data
{
	public class GameCreateInfo
	{
		public int Height { get; set; }
		public int Width { get; set; }
		public MeteoraViewBase View { get; set; }
		public string AppName { get; set; }
		public SDL.SDL_WindowFlags WindowFlags { get; set; }

		public GameCreateInfo()
		{
			Height = 1080;
			Width = 1920;
			View = new Meteora3DView();
			AppName = "Meteora Window";
			WindowFlags = SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN;
		}
	}
}
