using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Meteora.Data;

namespace Meteora.View
{
	public class MeteoraWindow : Form
	{

		public MeteoraControl control;
		protected IntPtr _handle;

		public MeteoraWindow(GameCreateInfo createInfo)
		{
			//Adjust for window border
			//width += 20;
			//height += 43;
			this.Size = new Size(createInfo.Width, createInfo.Height);
			this.StartPosition = FormStartPosition.CenterScreen;
			this.SizeGripStyle = SizeGripStyle.Hide;
			this.FormBorderStyle = createInfo.BorderStyle;
			this.MaximizeBox = false;
			this.MaximumSize = this.Size;
			this.Name = createInfo.Title;
			this.Text = createInfo.Title;
			this.Controls.Add(control = new MeteoraControl(createInfo.View, createInfo.Title) { Dock = DockStyle.Fill });
			_handle = control.Handle;
			FormClosing += (a, b) => control.OnClosing();
		}

		public virtual void DoMainLoop()
		{
			control.MainLoop();
		}

		public virtual void Init()
		{
			control.Init(_handle);
		}

	}
}
