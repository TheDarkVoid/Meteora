using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Meteora.View;

namespace Meteora.Data
{
	public class GameCreateInfo
	{
		public int Height { get; set; }
		public int Width { get; set; }
		public MeteoraViewBase View { get; set; }
		public string Title { get; set; }
		public FormBorderStyle BorderStyle { get; set; }

		public GameCreateInfo()
		{
			Height = 1080;
			Width = 1920;
			View = new MeteoraTriangleView();
			Title = "Meteora Window";
			BorderStyle = FormBorderStyle.FixedSingle;
		}
	}
}
