using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Meteora.View
{
	public class MeteoraWindow : Form
	{
		public MeteoraWindow(int width = 1280, int height = 720)
		{
			this.Size = new Size(width, height);
			this.StartPosition = FormStartPosition.CenterScreen;
			this.SizeGripStyle = SizeGripStyle.Hide;
			this.FormBorderStyle = FormBorderStyle.FixedSingle;
		}
	}
}
