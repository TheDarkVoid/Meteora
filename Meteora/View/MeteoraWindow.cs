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

		protected MeteoraControl _control;
		protected IntPtr _handle;

		public MeteoraWindow(MeteoraViewBase view, int width = 1280, int height = 720, string title = "Meteora Window", FormBorderStyle borderStyle = FormBorderStyle.FixedSingle)
		{
			//Adjust for window border
			//width += 20;
			//height += 43;
			this.Size = new Size(width, height);
			this.StartPosition = FormStartPosition.CenterScreen;
			this.SizeGripStyle = SizeGripStyle.Hide;
			this.FormBorderStyle = borderStyle;
			this.MaximizeBox = false;
			this.MaximumSize = this.Size;
			this.Name = title;
			this.Text = title;
			this.Controls.Add(_control = new MeteoraControl(view, title) { Dock = DockStyle.Fill });
			_handle = _control.Handle;
			FormClosing += (a, b) => _control.OnClosing();
		}

		public virtual void DoMainLoop()
		{
			_control.MainLoop();
		}

		public virtual void Init()
		{
			_control.Init(_handle);
		}

	}
}
