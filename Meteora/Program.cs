using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Meteora.View;
using Meteora.Data;
using GlmSharp;

public class Program
{
	static void Main(string[] args)
	{
		var game = new MeteoraGame(new GameCreateInfo
		{
			Height = 1080,
			Width = 1920,
		});
		game.Start();
	}
}
