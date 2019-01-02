using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meteora.Data
{
	public class Mesh
	{
		public Vertex[] vertices;
		public int[] indices;

		public Mesh()
		{

		}

		public Mesh(Vertex[] vertices, int[] indices)
		{
			this.vertices = vertices;
			this.indices = indices;
		}

		public static Mesh LoadObj(string path, float scale = 1f)
		{
			var lines = File.ReadAllLines(path);
			bool isVertex = false;
			int vertexCount = lines.Count(l => l[0] == 'v');
			int indexCount = lines.Count(l => l[0] == 'f') * 3;
			var mesh = new Mesh
			{
				vertices = new Vertex[vertexCount],
				indices = new int[indexCount]
			};
			int i = 0, v = 0;
			var rand = new Random();
			foreach (var line in lines)
			{
				if (line[0] == '#')
					continue;
				if(line[0] == 'o')
				{
					isVertex = true;
					continue;
				}
				if(line[0] == 's')
				{
					isVertex = false;
					continue;
				}
				var coords = line.Split(' ');
				if (coords.Length == 0)
					continue;
				if(isVertex)
				{
					float x = float.Parse(coords[1]), y = float.Parse(coords[2]), z = float.Parse(coords[3]);
					mesh.vertices[v++] = new Vertex
					{
						position = new Vector3(x * scale, y * scale, z * scale),
						color = new Vector3((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble())
					};
				}else
				{
					mesh.indices[i++] = int.Parse(coords[1]) - 1;
					mesh.indices[i++] = int.Parse(coords[2]) - 1;
					mesh.indices[i++] = int.Parse(coords[3]) - 1;
				}
			}
			return mesh;
		}
	}
}
