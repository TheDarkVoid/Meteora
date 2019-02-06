using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using GlmSharp;
using Meteora.Data;
using System.Numerics;
using Vulkan;
using SkiaSharp;

namespace Meteora.View
{
	public class Meteora3DView : MeteoraViewBase
	{
		private static readonly Vertex[] vertices = new Vertex[]
		{
			new Vertex(new float[] { -0.5f, -0.5f, 0f }, new float[] { 1.0f, 1.0f, 1.0f }, new float[] { 1.0f, 0.0f }),	//0 BL
			new Vertex(new float[] {  0.5f, -0.5f, 0f }, new float[] { 1.0f, 0.0f, 0.4f }, new float[] { 0.0f, 0.0f }),	//1 BR
			new Vertex(new float[] {  0.5f,  0.5f, 0f }, new float[] { 1.0f, 1.0f, 1.0f }, new float[] { 0.0f, 1.0f }),	//2 TR
			new Vertex(new float[] { -0.5f,  0.5f, 0f }, new float[] { 1.0f, 0.0f, 0.4f }, new float[] { 1.0f, 1.0f }),	//3 TL

			new Vertex(new float[] { -0.5f, -0.5f, 1f }, new float[] { 1.0f, 1.0f, 1.0f }, new float[] { 1.0f, 0.0f }),	//4 BL
			new Vertex(new float[] {  0.5f, -0.5f, 1f }, new float[] { 1.0f, 0.0f, 0.4f }, new float[] { 0.0f, 0.0f }),	//5 BR
			new Vertex(new float[] {  0.5f,  0.5f, 1f }, new float[] { 1.0f, 1.0f, 1.0f }, new float[] { 0.0f, 1.0f }),	//6 TR
			new Vertex(new float[] { -0.5f,  0.5f, 1f }, new float[] { 1.0f, 0.0f, 0.4f }, new float[] { 1.0f, 1.0f }),	//7 TL
		};

		private static readonly int[] indices = new[]
		{
			//Front
			2, 3, 7, 7, 6, 2,
			//Back
			4, 0, 1, 1, 5, 4,
			//Left
			3, 0, 4, 4, 7, 3,
			//Right
			5, 1, 2, 2, 6, 5,
			//Top
			4, 5, 6, 6, 7, 4,
			//Bottom
			2, 1, 0, 0, 3, 2,
		};

		//private Mesh mesh = new Mesh(vertices, indices);
		private Mesh mesh = Mesh.LoadObj(@"Res/Models/cube.obj");
		//private Mesh mesh = Mesh.LoadObj(@"Res/Models/sphere.obj");
		//private Mesh mesh = Mesh.LoadObj(@"Res/Models/sphereIco.obj");
		//private Mesh mesh = Mesh.LoadObj(@"Res/Models/cone.obj");
		//private Mesh mesh = Mesh.LoadObj(@"Res/Models/monkey.obj");

		//Textures
		protected Image textureImage;
		protected ImageView textureImageView;
		protected DeviceMemory textureImageMemory;
		protected Sampler textureSampler;

		protected DescriptorSetLayout descriptorSetLayout;
		protected DescriptorPool descriptorPool;
		protected DescriptorSet[] descriptorSets;

		//Buffers
		protected Vulkan.Buffer vertexBuffer;
		protected DeviceMemory vertexBufferMemory;
		protected Vulkan.Buffer stagingBuffer;
		protected DeviceMemory stagingBufferMemory;
		protected Vulkan.Buffer indexBuffer;
		protected DeviceMemory indexBufferMemory;
		protected Vulkan.Buffer[] uniformBuffers;
		protected DeviceMemory[] unifromBuffersMemory;



		public const float DegToRad = (float)Math.PI / 180f;

		protected override PipelineShaderStageCreateInfo[] CreateShaderStages()
		{
			var fragModule = CreateShaderModule(File.ReadAllBytes(@"Res/Shaders/Fragment/frag.spv"));
			var vertModule = CreateShaderModule(File.ReadAllBytes(@"Res/Shaders/Vertex/vert.spv"));

			var vertShaderStageInfo = new PipelineShaderStageCreateInfo
			{
				Stage = ShaderStageFlags.Vertex,
				Module = vertModule,
				Name = "main"
			};

			var fragShaderStageInfo = new PipelineShaderStageCreateInfo
			{
				Stage = ShaderStageFlags.Fragment,
				Module = fragModule,
				Name = "main"
			};

			var shaderStages = new PipelineShaderStageCreateInfo[]
			{
				vertShaderStageInfo,
				fragShaderStageInfo
			};

			return shaderStages;
		}
		double angle;

		protected override void Start()
		{
			angle = 0;
		}

		protected override void Draw(uint curImage)
		{
			UpdateUniformBuffer(curImage);
		}


		public void UpdateUniformBuffer(uint currentImage)
		{
			if (angle > 360)
				angle -= 360;
			angle += DeltaTime.TotalSeconds * 45f;
			var ubo = new UniformBufferObject
			{
				model = mat4.Identity * mat4.Rotate(glm.Radians((float)angle), vec3.UnitY),
				view = mat4.Translate(0,0,-5),
				proj = mat4.Perspective(glm.Radians(90f), extent.Width / (float)extent.Height, .1f, 10f)
			};

			ubo.proj[1, 1] *= -1;
			var values = ubo.Values;
			var dst = device.MapMemory(unifromBuffersMemory[currentImage], 0, values.Length);
			Marshal.Copy(values, 0, dst, values.Length);
			device.UnmapMemory(unifromBuffersMemory[currentImage]);
		}

		protected override void InitCommandBuffers()
		{
			for (int i = 0; i < bufferSize; i++)
			{
				var beginInfo = new CommandBufferBeginInfo
				{
					Flags = CommandBufferUsageFlags.SimultaneousUse
				};
				commandBuffers[i].Begin(beginInfo);

				var clearColor = new ClearValue
				{
					Color = new ClearColorValue(new float[] { 25/255f, 0.0f, 10/255f, 1.0f } )
				};
				var renderPassInfo = new RenderPassBeginInfo
				{
					RenderPass = renderPass,
					Framebuffer = framebuffers[i],
					RenderArea = new Rect2D
					{
						Offset = new Offset2D
						{
							X = 0,
							Y = 0
						},
						Extent = extent
					},
					ClearValueCount = 1,
					ClearValues = new ClearValue[] { clearColor }
				};
				commandBuffers[i].CmdBeginRenderPass(renderPassInfo, SubpassContents.Inline);
				commandBuffers[i].CmdBindPipeline(PipelineBindPoint.Graphics, graphicsPipeline);

				commandBuffers[i].CmdBindVertexBuffer(0, vertexBuffer, 0);
				commandBuffers[i].CmdBindIndexBuffer(indexBuffer, 0, IndexType.Uint32);

				commandBuffers[i].CmdBindDescriptorSet(PipelineBindPoint.Graphics, pipelineLayout, 0, descriptorSets[i], null);

				commandBuffers[i].CmdDrawIndexed((uint)mesh.indices.Length, 1, 0, 0, 0);

				commandBuffers[i].CmdEndRenderPass();
				commandBuffers[i].End();
			}
		}

		

		protected override PipelineVertexInputStateCreateInfo GetVertexInputInfo()
		{
			var bindingDesc = Vertex.GetBindingDescription();
			var attrDesc = Vertex.GetAttributeDescriptions();

			var vertexInputInfo = new PipelineVertexInputStateCreateInfo
			{
				VertexAttributeDescriptionCount = (uint)bindingDesc.Length,
				VertexBindingDescriptionCount = (uint)attrDesc.Length,
				VertexBindingDescriptions = bindingDesc,
				VertexAttributeDescriptions = attrDesc
			};

			return vertexInputInfo;
		}

		protected override void CreateBuffers()
		{
			CreateTextureImage();
			CreateTextureImageView();
			CreateTextureSampler();
			CreateVertexBuffer();
			CreateIndexBuffer();
			CreateUniformBuffer();
		}


		protected void CreateTextureImage()
		{
			using (var image = SKBitmap.Decode(@"Res/Textures/Rin.png"))
			{
				var pixels = image.Pixels.SelectMany(c => new float[] { c.Red, c.Green, c.Blue, c.Alpha }).ToArray();
				(stagingBuffer, stagingBufferMemory) = CreateBuffer(pixels, BufferUsageFlags.TransferSrc, MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent);
				(textureImage, textureImageMemory) = CreateImage(image.Width, image.Height);

				TransitionImageLayout(textureImage, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);
				CopyBufferToImage(stagingBuffer, textureImage, image.Width, image.Height);
				TransitionImageLayout(textureImage, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);
			}
			device.DestroyBuffer(stagingBuffer);
			device.FreeMemory(stagingBufferMemory);
		}
		protected void CreateTextureImageView()
		{
			textureImageView = CreateImageView(textureImage);
		}

		protected void CreateTextureSampler()
		{
			var samplerInfo = new SamplerCreateInfo
			{
				MagFilter = Filter.Linear,
				MinFilter = Filter.Linear,
				AddressModeU = SamplerAddressMode.Repeat,
				AddressModeV = SamplerAddressMode.Repeat,
				AddressModeW = SamplerAddressMode.Repeat,
				AnisotropyEnable = true,
				MaxAnisotropy = 16,
				BorderColor = BorderColor.IntOpaqueBlack,
				UnnormalizedCoordinates = false,
				CompareEnable = false,
				CompareOp = CompareOp.Always,
				MipmapMode = SamplerMipmapMode.Linear,
			};

			textureSampler = device.CreateSampler(samplerInfo);
		}

		protected void CreateVertexBuffer()
		{
			var vtx = mesh.vertices.SelectMany(v => v.Data).ToArray();
			var bufferSize = mesh.vertices.Length * Vertex.SIZE;


			var (stagingBuffer, stagingBufferMemory) = CreateBuffer(vtx, BufferUsageFlags.TransferSrc, MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent);

			(vertexBuffer, vertexBufferMemory) = CreateBuffer(bufferSize, BufferUsageFlags.TransferDst | BufferUsageFlags.VertexBuffer, MemoryPropertyFlags.DeviceLocal);

			CopyBuffer(stagingBuffer, vertexBuffer, bufferSize);

			device.DestroyBuffer(stagingBuffer);
			device.FreeMemory(stagingBufferMemory);
		}

		protected void CreateIndexBuffer()
		{
			var size = sizeof(int) * mesh.indices.Length;

			var (stagingBuffer, stagingBufferMemory) = CreateBuffer(mesh.indices, BufferUsageFlags.TransferSrc, MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent);

			(indexBuffer, indexBufferMemory) = CreateBuffer(size, BufferUsageFlags.TransferDst | BufferUsageFlags.IndexBuffer, MemoryPropertyFlags.DeviceLocal);

			CopyBuffer(stagingBuffer, indexBuffer, size);

			device.DestroyBuffer(stagingBuffer);
			device.FreeMemory(stagingBufferMemory);
		}

		protected void CreateUniformBuffer()
		{
			var size = sizeof(float) * 4 * 4 * 3;

			uniformBuffers = new Vulkan.Buffer[images.Length];
			unifromBuffersMemory = new DeviceMemory[images.Length];

			for (int i = 0; i < images.Length; i++)
			{
				(uniformBuffers[i], unifromBuffersMemory[i]) = CreateBuffer(size, BufferUsageFlags.UniformBuffer, MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent);
			}
		}

		protected override void CreateDescriptorPool()
		{
			var poolSizes = new DescriptorPoolSize[]
			{
				new DescriptorPoolSize
				{
					Type = DescriptorType.UniformBuffer,
					DescriptorCount = (uint)images.Length,
				},
				new DescriptorPoolSize
				{
					Type = DescriptorType.CombinedImageSampler,
					DescriptorCount = (uint)images.Length
				}
			};

			DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo
			{
				PoolSizeCount = (uint)poolSizes.Length,
				PoolSizes = poolSizes,
				MaxSets = (uint)images.Length
			};
			descriptorPool = device.CreateDescriptorPool(poolInfo);
		}

		protected override void CreateDescriptorSets()
		{
			var layouts = new DescriptorSetLayout[images.Length];
			for (int i = 0; i < layouts.Length; i++)
				layouts[i] = descriptorSetLayout;
			var allocInfo = new DescriptorSetAllocateInfo
			{
				DescriptorPool = descriptorPool,
				DescriptorSetCount = (uint)images.Length,
				SetLayouts = layouts
			};

			descriptorSets = device.AllocateDescriptorSets(allocInfo);
			for (int a = 0; a < descriptorSets.Length; a++)
			{
				var bufferInfo = new DescriptorBufferInfo
				{
					Buffer = uniformBuffers[a],
					Offset = 0,
					Range = (sizeof(float) * 16) * 3
				};

				var imageInfo = new DescriptorImageInfo
				{
					ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
					ImageView = textureImageView,
					Sampler = textureSampler
				};

				var descriptorWrites = new WriteDescriptorSet[]
				{
					new WriteDescriptorSet
					{
						DstSet = descriptorSets[a],
						DstBinding = 0,
						DstArrayElement = 0,
						DescriptorType = DescriptorType.UniformBuffer,
						DescriptorCount = 1,
						BufferInfo = new DescriptorBufferInfo[] { bufferInfo }
					},
					new WriteDescriptorSet
					{
						DstSet = descriptorSets[a],
						DstBinding = 1,
						DstArrayElement = 0,
						DescriptorType = DescriptorType.CombinedImageSampler,
						DescriptorCount = 1,
						ImageInfo = new DescriptorImageInfo[] { imageInfo }
					}
				};
				//device.UpdateDescriptorSet(descriptorWrites[0], null);
				//device.UpdateDescriptorSet(descriptorWrites[1], null);
				device.UpdateDescriptorSets(descriptorWrites, null);
			}

		}

		protected override void CreateDescriptorSetLayout()
		{
			var uboLayoutBinding = new DescriptorSetLayoutBinding
			{
				Binding = 0,
				DescriptorType = DescriptorType.UniformBuffer,
				DescriptorCount = 1,
				ImmutableSamplers = null,
				StageFlags = ShaderStageFlags.Vertex
			};

			var samplerLayoutBinding = new DescriptorSetLayoutBinding
			{
				Binding = 1,
				DescriptorCount = 1,
				DescriptorType = DescriptorType.CombinedImageSampler,
				ImmutableSamplers = null,
				StageFlags = ShaderStageFlags.Fragment
			};

			var bindings = new[] { uboLayoutBinding, samplerLayoutBinding };

			var layoutInfo = new DescriptorSetLayoutCreateInfo
			{
				BindingCount = (uint)bindings.Length,
				Bindings = bindings,
			};
			descriptorSetLayout = device.CreateDescriptorSetLayout(layoutInfo);
		}

		protected override PipelineLayoutCreateInfo GetPipelineLayoutInfo() => new PipelineLayoutCreateInfo
		{
			SetLayoutCount = 1,
			SetLayouts = new[] { descriptorSetLayout }
		};

		public override void CleanupBuffers()
		{
			device.DestroyDescriptorPool(descriptorPool);
			device.DestroyDescriptorSetLayout(descriptorSetLayout);
			for (int i = 0; i < uniformBuffers.Length; i++)
			{
				device.DestroyBuffer(uniformBuffers[i]);
				device.FreeMemory(unifromBuffersMemory[i]);
			}
			device.DestroyBuffer(vertexBuffer);
			device.DestroyBuffer(indexBuffer);
			device.DestroySampler(textureSampler);
			device.DestroyImageView(textureImageView);
			device.DestroyImage(textureImage);
			device.FreeMemory(textureImageMemory);
			device.FreeMemory(vertexBufferMemory);
			device.FreeMemory(indexBufferMemory);
		}
	}
}
