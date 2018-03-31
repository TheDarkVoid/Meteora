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
		public MeteoraWindow(int width = 1280, int height = 720, string title = "Meteora Window")
		{
			this.Size = new Size(width, height);
			this.Name = title;
			this.StartPosition = FormStartPosition.CenterScreen;
			this.SizeGripStyle = SizeGripStyle.Hide;
			this.FormBorderStyle = FormBorderStyle.FixedSingle;
			this.Controls.Add(new MeteoraControl(new MeteoraClearView()) { Dock = DockStyle.Fill });
		}
	}
}
