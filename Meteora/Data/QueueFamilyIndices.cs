using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meteora.Data
{
	public class QueueFamilyIndices
	{
		public int GraphicsFamily { get; set; } = -1;
		public int PresentFamily { get; set; } = -1;

		public bool isComplete() => GraphicsFamily >= 0 && PresentFamily >= 0;
	}
}
