using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Meteora.View
{
	public class MeteoraWindow : Form
	{

		public MeteoraWindow(MeteoraViewBase view, int width = 1280, int height = 720, string title = "Meteora Window")
		{
			//Adjust for window border
			width += 20;
			height += 43;
			this.Size = new Size(width, height);
			this.StartPosition = FormStartPosition.CenterScreen;
			//this.SizeGripStyle = SizeGripStyle.Hide;
			this.FormBorderStyle = FormBorderStyle.Fixed3D;
			this.MaximizeBox = false;
			this.MaximumSize = this.Size;
			this.Name = title;
			this.Text = title;
			MeteoraControl control;
			this.Controls.Add(control = new MeteoraControl(view, title) { Dock = DockStyle.Fill });
			FormClosing += (a, b) => control.OnClosing();
		}
	}
}
