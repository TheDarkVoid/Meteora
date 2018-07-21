﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Meteora;
using System.Windows.Forms;
using System.Threading;
using Meteora.View;
using Vulkan;
using System.Runtime.InteropServices;
using System.IO;
using Meteora.Data;

class Program
{
	static void Main(string[] args)
	{
		var game = new MeteoraGame(new GameCreateInfo
		{
			Height = 1080,
			Width = 1920
		});
		game.Start();
	}
}
