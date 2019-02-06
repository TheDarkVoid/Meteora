using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vulkan.Windows;
using Vulkan;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Meteora.Data;
using System.IO;
using System.Windows.Threading;
using System.Timers;
using SDL2;

namespace Meteora.View
{
	public abstract class MeteoraViewBase : IMeteoraView
	{
		public const uint VK_SUBPASS_INTERNAL = ~0U;
		public const uint VK_QUEUE_FAMILY_IGNORED = ~0U;
		public const int MAX_FRAMES_IN_FLIGHT = 2;

		public bool initialized;
		public object runLock = new object();
		public bool running;
		public bool render = true;
		public Device device;
		public double FPS;
		public TimeSpan DeltaTime { get; private set; }

		protected InstanceCreateData data;
		protected Queue graphicsQueue;
		protected Queue presentQueue;
		protected SwapchainKhr swapchain;
		protected Image[] images;
		protected ImageView[] imageViews;
		protected Extent2D extent;
		protected SurfaceFormatKhr format;
		protected RenderPass renderPass;
		protected PipelineLayout pipelineLayout;
		protected Pipeline graphicsPipeline;
		protected Framebuffer[] framebuffers;
		protected CommandPool commandPool;
		protected CommandBuffer[] commandBuffers;
		protected Semaphore[] imageAvailableSemaphore;
		protected Semaphore[] renderFinishedSemaphore;
		protected uint bufferSize;
		protected Fence[] inflightFences;
		protected int currentFrame = 0;

		private readonly Semaphore[] waitSemaphores = new Semaphore[1];
		private readonly Semaphore[] signalSemaphores = new Semaphore[1];
		private readonly CommandBuffer[] renderCommandBuffers = new CommandBuffer[1];
		private readonly SwapchainKhr[] renderSwapchains = new SwapchainKhr[1];
		private readonly uint[] renderImageIndices = new uint[1];
		private SubmitInfo submitInfo;
		private PresentInfoKhr presentInfo;
		private PipelineStageFlags[] waitStages = { PipelineStageFlags.ColorAttachmentOutput };
		private int frameCount;
		private DateTime nextSecond = DateTime.Now;

		private DateTime _lastFrame;

		#region Init
		public virtual void Initialize(InstanceCreateData data)
		{
			this.data = data;
			var graphicsQueue = new DeviceQueueCreateInfo
			{
				QueuePriorities = new float[] { 1.0f },
				QueueFamilyIndex = (uint)data.queueFamilyIndices.GraphicsFamily
			};
			var presentQueue = new DeviceQueueCreateInfo
			{
				QueuePriorities = new float[] { 1.0f },
				QueueFamilyIndex = (uint)data.queueFamilyIndices.PresentFamily
			};
			var deviceInfo = new DeviceCreateInfo
			{
				EnabledExtensionNames = data.enabledDeviceExtensions,
				EnabledExtensionCount = (uint)data.enabledDeviceExtensions.Length,
				EnabledLayerNames = data.enabledLayers,
				EnabledLayerCount = (data.enabledLayers == null ? 0 : (uint)data.enabledLayers.Length),
				QueueCreateInfos = new DeviceQueueCreateInfo[] { graphicsQueue, presentQueue },
				EnabledFeatures = new PhysicalDeviceFeatures
				{
					SamplerAnisotropy = true
				}
			};

			device = data.physicalDevice.CreateDevice(deviceInfo);
			this.graphicsQueue = device.GetQueue((uint)data.queueFamilyIndices.GraphicsFamily, 0);
			this.presentQueue = device.GetQueue((uint)data.queueFamilyIndices.PresentFamily, 0);

			CreateSwapChain();
			CreateImageViews();
			CreateRenderPass();
			CreateDescriptorSetLayout();
			CreateGraphicsPipeline();
			CreateFrameBuffers();
			CreateCommandPool();
			CreateBuffers();
			CreateDescriptorPool();
			CreateDescriptorSets();
			CreateCommandBuffers();
			CreateSyncObjects();

			submitInfo = new SubmitInfo
			{
				WaitSemaphoreCount = 1,
				WaitDstStageMask = waitStages,
				CommandBufferCount = 1,
				SignalSemaphoreCount = 1,
			};
			presentInfo = new PresentInfoKhr
			{
				WaitSemaphoreCount = 1,
				SwapchainCount = 1,
			};

			initialized = running = true;
			_lastFrame = DateTime.Now;
			Start();

			//data.control.ParentForm.ResizeBegin += ResizeBegin;
			//data.control.ParentForm.ResizeEnd += ResizeEnd;
		}

		#region Resize
		protected virtual void ResizeBegin(object sender, EventArgs e)
		{
			render = false;
		}

		protected virtual void ResizeEnd(object sender, EventArgs e)
		{
			RecreateSwapChain();
		}
		#endregion

		#endregion

		#region Start
		protected virtual void Start()
		{
		}
		#endregion

		#region Draw
		public virtual void DrawFrame()
		{
			//if (!initialized)
			//return;
			if (!render)
				return;
			//if (!Running)
			//return;
			device.WaitForFence(inflightFences[currentFrame], true, ulong.MaxValue);
			device.ResetFence(inflightFences[currentFrame]);
			DeltaTime = (DateTime.Now - _lastFrame);
			_lastFrame = DateTime.Now;
			if (DateTime.Now >= nextSecond)
			{
				var frameTime = (DateTime.Now - nextSecond.AddSeconds(-1)).TotalSeconds;
				FPS = Math.Round(frameCount / frameTime);
				nextSecond = DateTime.Now.AddSeconds(1);
				frameCount = 0;
				Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 1);
				Console.Write($"{FPS}fps {DeltaTime.TotalMilliseconds}ms ");
			}
			frameCount++;
			try
			{
				var imageIndex = device.AcquireNextImageKHR(swapchain, ulong.MaxValue, imageAvailableSemaphore[currentFrame]);
				waitSemaphores[0] = imageAvailableSemaphore[currentFrame];
				signalSemaphores[0] = renderFinishedSemaphore[currentFrame];

				Draw(imageIndex);

				submitInfo.WaitSemaphores = waitSemaphores;
				submitInfo.SignalSemaphores = signalSemaphores;
				renderCommandBuffers[0] = commandBuffers[imageIndex];
				submitInfo.CommandBuffers = renderCommandBuffers;

				graphicsQueue.Submit(submitInfo, inflightFences[currentFrame]);
				renderSwapchains[0] = swapchain;
				presentInfo.Swapchains = renderSwapchains;
				renderImageIndices[0] = imageIndex;
				presentInfo.ImageIndices = renderImageIndices;
				presentInfo.WaitSemaphores = signalSemaphores;

				presentQueue.PresentKHR(presentInfo);
			}
			catch (ResultException e)
			{
				if (e.Result == Result.ErrorOutOfDateKhr || e.Result == Result.SuboptimalKhr)
				{
					return;
				}
				else
					throw e;
			}
			currentFrame = (currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
		}

		protected virtual void Draw(uint curImage)
		{

		}
		#endregion

		#region Swapchain
		protected SurfaceFormatKhr ChooseFormat(SurfaceFormatKhr[] supportedFormats)
		{
			if (supportedFormats.Length == 1 && supportedFormats[0].Format == Format.Undefined)
				return new SurfaceFormatKhr
				{
					ColorSpace = ColorSpaceKhr.SrgbNonlinear,
					Format = Format.R8G8B8A8Unorm
				};

			foreach (var f in supportedFormats)
				if (f.Format == Format.R8G8B8A8Unorm || f.Format == Format.B8G8R8A8Unorm)
					return f;
			return supportedFormats[0];
		}

		protected PresentModeKhr ChoosePresentMode(PresentModeKhr[] presentModes)
		{
			foreach (PresentModeKhr mode in presentModes)
			{
				if (mode == PresentModeKhr.Mailbox)
					return mode;
				else if (mode == PresentModeKhr.Immediate)
					return mode;
			}
			return PresentModeKhr.Fifo;
		}

		protected Extent2D ChooseSwapExtent()
		{
			//return capabilities.CurrentExtent;
			SDL.SDL_GetWindowSize(data.windowPtr, out int w, out int h);
			return new Extent2D
			{
				Height = (uint)h,
				Width = (uint)w
			};
		}

		protected void CreateSwapChain()
		{
			format = ChooseFormat(data.formats);
			var presentMode = ChoosePresentMode(data.presentModes);
			extent = ChooseSwapExtent();

			var imageCount = data.surfaceCapabilities.MinImageCount + 1;
			if (data.surfaceCapabilities.MaxImageCount > 0 && imageCount > data.surfaceCapabilities.MaxImageCount)
				imageCount = data.surfaceCapabilities.MaxImageCount;

			var swapChainInfo = new SwapchainCreateInfoKhr
			{
				Surface = data.surface,
				MinImageCount = imageCount,
				ImageFormat = format.Format,
				ImageColorSpace = format.ColorSpace,
				ImageExtent = extent,
				ImageArrayLayers = 1,
				ImageUsage = ImageUsageFlags.ColorAttachment,
				PreTransform = data.surfaceCapabilities.CurrentTransform,
				CompositeAlpha = CompositeAlphaFlagsKhr.Opaque,
				PresentMode = presentMode,
				Clipped = true
			};
			var indices = data.queueFamilyIndices;
			var queueFamilyIndices = new uint[] { (uint)indices.GraphicsFamily, (uint)indices.PresentFamily };
			if (indices.PresentFamily != indices.GraphicsFamily)
			{
				swapChainInfo.ImageSharingMode = SharingMode.Concurrent;
				swapChainInfo.QueueFamilyIndexCount = (uint)queueFamilyIndices.Length;
				swapChainInfo.QueueFamilyIndices = queueFamilyIndices;
			}
			else
				swapChainInfo.ImageSharingMode = SharingMode.Exclusive;

			swapchain = device.CreateSwapchainKHR(swapChainInfo);

			images = device.GetSwapchainImagesKHR(swapchain);
			bufferSize = imageCount;
		}

		protected void CleanupSwapChain()
		{
			for (int i = 0; i < bufferSize; i++)
				device.DestroyFramebuffer(framebuffers[i]);

			device.FreeCommandBuffers(commandPool, commandBuffers);
			device.DestroyPipeline(graphicsPipeline);
			device.DestroyPipelineLayout(pipelineLayout);
			device.DestroyRenderPass(renderPass);

			for (int i = 0; i < bufferSize; i++)
				device.DestroyImageView(imageViews[i]);

			device.DestroySwapchainKHR(swapchain);
		}

		protected void RecreateSwapChain()
		{
			if (render)
				return;
			device.WaitIdle();

			CleanupSwapChain();

			CreateSwapChain();
			CreateImageViews();
			CreateRenderPass();
			CreateGraphicsPipeline();
			CreateFrameBuffers();
			CreateCommandBuffers();

			render = true;
		}
		#endregion

		#region Image Views
		protected void CreateImageViews()
		{
			imageViews = new ImageView[bufferSize];
			for (int i = 0; i < images.Length; i++)
			{
				imageViews[i] = CreateImageView(images[i], format.Format); 
			}
		}

		protected ImageView CreateImageView(Image image, Format format = Format.R8G8B8A8Unorm)
		{
			var viewInfo = new ImageViewCreateInfo
			{
				Image = image,
				ViewType = ImageViewType.View2D,
				Format = format,
				SubresourceRange = new ImageSubresourceRange
				{
					AspectMask = ImageAspectFlags.Color,
					BaseMipLevel = 0,
					LevelCount = 1,
					BaseArrayLayer = 0,
					LayerCount = 1
				}
			};

			return device.CreateImageView(viewInfo);
		}
		#endregion

		#region Descriptor Pool
		protected virtual void CreateDescriptorPool()
		{

		}
		#endregion

		#region Descriptor Sets
		protected virtual void CreateDescriptorSets()
		{

		}
		#endregion

		#region Descriptor Set Layout
		protected virtual void CreateDescriptorSetLayout()
		{
			
		}
		#endregion

		#region Graphics Pipeline
		protected virtual void CreateGraphicsPipeline()
		{
			var shaderStages = CreateShaderStages();

			var vertexInputInfo = GetVertexInputInfo();

			var inputAssembly = new PipelineInputAssemblyStateCreateInfo
			{
				Topology = PrimitiveTopology.TriangleList,
				PrimitiveRestartEnable = false
			};

			var viewport = new Viewport
			{
				X = 0f,
				Y = 0f,
				Width = extent.Width,
				Height = extent.Height,
				MinDepth = 0f,
				MaxDepth = 1f
			};

			var scissor = new Rect2D
			{
				Offset = new Offset2D
				{
					X = 0,
					Y = 0
				},
				Extent = extent
			};

			var viewportState = new PipelineViewportStateCreateInfo
			{
				ViewportCount = 1,
				Viewports = new Viewport[] { viewport },
				ScissorCount = 1,
				Scissors = new Rect2D[] { scissor }
			};

			var rasterizer = new PipelineRasterizationStateCreateInfo
			{
				DepthClampEnable = false,
				RasterizerDiscardEnable = false,
				PolygonMode = PolygonMode.Fill,
				LineWidth = 1f,
				CullMode = CullModeFlags.Back,
				FrontFace = FrontFace.CounterClockwise,
				DepthBiasEnable = false,
				DepthBiasConstantFactor = 0f,
				DepthBiasClamp = 0f,
				DepthBiasSlopeFactor = 0f
				
			};

			var msaa = new PipelineMultisampleStateCreateInfo
			{
				SampleShadingEnable = false,
				RasterizationSamples = SampleCountFlags.Count1,
				MinSampleShading = 1f,
				AlphaToCoverageEnable = false,
				AlphaToOneEnable = false
			};

			var colorBlendAttachment = new PipelineColorBlendAttachmentState
			{
				ColorWriteMask = ColorComponentFlags.R | ColorComponentFlags.G | ColorComponentFlags.B | ColorComponentFlags.A,
				BlendEnable = false,
				SrcColorBlendFactor = BlendFactor.One,
				DstColorBlendFactor = BlendFactor.Zero,
				ColorBlendOp = BlendOp.Add,
				SrcAlphaBlendFactor = BlendFactor.One,
				DstAlphaBlendFactor = BlendFactor.Zero,
				AlphaBlendOp = BlendOp.Add
			};

			var colorBlending = new PipelineColorBlendStateCreateInfo
			{
				LogicOpEnable = false,
				LogicOp = LogicOp.Copy,
				AttachmentCount = 1,
				Attachments = new PipelineColorBlendAttachmentState[] { colorBlendAttachment },
			};

			var layoutInfo = GetPipelineLayoutInfo();

			pipelineLayout = device.CreatePipelineLayout(layoutInfo);

			var pipelineCreateInfo = new GraphicsPipelineCreateInfo
			{
				StageCount = (shaderStages == null ? 0 : (uint)shaderStages.Length),
				Stages = shaderStages,
				VertexInputState = vertexInputInfo,
				InputAssemblyState = inputAssembly,
				ViewportState = viewportState,
				RasterizationState = rasterizer,
				MultisampleState = msaa,
				ColorBlendState = colorBlending,
				Layout = pipelineLayout,
				RenderPass = renderPass,
				Subpass = 0,
				BasePipelineHandle = null,
				BasePipelineIndex = -1
			};

			graphicsPipeline = device.CreateGraphicsPipelines(null, new GraphicsPipelineCreateInfo[] { pipelineCreateInfo })[0];

			if (shaderStages == null)
				return;
			foreach (var stage in shaderStages)
				device.DestroyShaderModule(stage.Module);
		}

		protected virtual PipelineLayoutCreateInfo GetPipelineLayoutInfo()
		{
			return new PipelineLayoutCreateInfo();
		}

		protected virtual PipelineVertexInputStateCreateInfo GetVertexInputInfo()
		{
			var vertexInputInfo = new PipelineVertexInputStateCreateInfo
			{
				VertexAttributeDescriptionCount = 0,
				VertexBindingDescriptionCount = 0
			};
			return vertexInputInfo;
		}

		protected virtual PipelineShaderStageCreateInfo[] CreateShaderStages()
		{
			return null;
		}

		protected ShaderModule CreateShaderModule(byte[] code)
		{
			var shaderInfo = new ShaderModuleCreateInfo
			{
				CodeSize = (UIntPtr)code.Length,
				CodeBytes = code
			};
			return device.CreateShaderModule(shaderInfo);
		}
		#endregion

		#region Render Pass
		protected void CreateRenderPass()
		{
			var colorAttachment = new AttachmentDescription
			{
				Format = format.Format,
				Samples = SampleCountFlags.Count1,
				LoadOp = AttachmentLoadOp.Clear,
				StoreOp = AttachmentStoreOp.Store,
				StencilLoadOp = AttachmentLoadOp.DontCare,
				StencilStoreOp = AttachmentStoreOp.DontCare,
				InitialLayout = ImageLayout.Undefined,
				FinalLayout = ImageLayout.PresentSrcKhr
			};

			var colorAttachmentRef = new AttachmentReference
			{
				Attachment = 0,
				Layout = ImageLayout.ColorAttachmentOptimal
			};

			var subpass = new SubpassDescription
			{
				PipelineBindPoint = PipelineBindPoint.Graphics,
				ColorAttachmentCount = 1,
				ColorAttachments = new AttachmentReference[] { colorAttachmentRef },
			};

			var dependency = new SubpassDependency
			{
				SrcSubpass = VK_SUBPASS_INTERNAL,
				DstSubpass = 0,
				SrcStageMask = PipelineStageFlags.ColorAttachmentOutput,
				SrcAccessMask = 0,
				DstStageMask = PipelineStageFlags.ColorAttachmentOutput,
				DstAccessMask = AccessFlags.ColorAttachmentRead | AccessFlags.ColorAttachmentWrite
			};

			var renderPassInfo = new RenderPassCreateInfo
			{
				AttachmentCount = 1,
				Attachments = new AttachmentDescription[] { colorAttachment },
				SubpassCount = 1,
				Subpasses = new SubpassDescription[] { subpass },
				DependencyCount = 1,
				Dependencies = new SubpassDependency[] { dependency }
			};

			renderPass = device.CreateRenderPass(renderPassInfo);
		}
		#endregion

		#region Frame Buffers
		protected void CreateFrameBuffers()
		{
			framebuffers = new Framebuffer[bufferSize];
			for (int i = 0; i < bufferSize; i++)
			{
				ImageView[] attachments = { imageViews[i] };

				var frameBufferInfo = new FramebufferCreateInfo
				{
					RenderPass = renderPass,
					AttachmentCount = 1,
					Attachments = attachments,
					Width = extent.Width,
					Height = extent.Height,
					Layers = 1
				};

				framebuffers[i] = device.CreateFramebuffer(frameBufferInfo);
			}
		}
		#endregion

		#region Command Pool
		protected void CreateCommandPool()
		{
			var poolInfo = new CommandPoolCreateInfo
			{
				QueueFamilyIndex = (uint)data.queueFamilyIndices.GraphicsFamily
			};
			commandPool = device.CreateCommandPool(poolInfo);
		}
		#endregion

		#region Buffers

		protected virtual void CreateBuffers()
		{

		}

		protected virtual uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
		{
			var memProperties = data.physicalDevice.GetMemoryProperties();
			for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
			{
				if (((typeFilter >> (int)i) & 1) == 1 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
					return i;
			}
			throw new Exception("Unable to find suiable memory type");
		}
		#endregion

		#region Memory Buffers
		public (Vulkan.Buffer buffer, DeviceMemory memory) CreateBuffer<T>(T[] data, BufferUsageFlags usage, MemoryPropertyFlags properties)
		{
			DeviceSize size = 0;
			switch(data)
			{
				case float[] _:
					size = sizeof(float) * data.Length;
					break;
				case double[] _:
					size = sizeof(double) * data.Length;
					break;
				case short[] _:
					size = sizeof(short) * data.Length;
					break;
				case int[] _:
					size = sizeof(int) * data.Length;
					break;
				case long[] _:
					size = sizeof(long) * data.Length;
					break;
				case ushort[] _:
					size = sizeof(ushort) * data.Length;
					break;
				case uint[] _:
					size = sizeof(uint) * data.Length;
					break;
				case ulong[] _:
					size = sizeof(ulong) * data.Length;
					break;
				default:
					throw new Exception("Only float, double, short, int, and long are supported");
			}


			var (buffer, memory) = CreateBuffer(size, usage, properties);
			var memPtr = device.MapMemory(memory, 0, size);
			switch(data)
			{
				case float[] bufferData:
					Marshal.Copy(bufferData, 0, memPtr, data.Length);
					break;
				case double[] bufferData:
					Marshal.Copy(bufferData, 0, memPtr, data.Length);
					break;
				case short[] bufferData:
					Marshal.Copy(bufferData, 0, memPtr, data.Length);
					break;
				case int[] bufferData:
					Marshal.Copy(bufferData, 0, memPtr, data.Length);
					break;
				case long[] bufferData:
					Marshal.Copy(bufferData, 0, memPtr, data.Length);
					break;
				case ushort[] bufferData:
					Marshal.Copy(bufferData.Select(e => (short)e).ToArray(), 0, memPtr, data.Length);
					break;
				case uint[] bufferData:
					Marshal.Copy(bufferData.Select(e => (int)e).ToArray(), 0, memPtr, data.Length);
					break;
				case ulong[] bufferData:
					Marshal.Copy(bufferData.Select(e => (long)e).ToArray(), 0, memPtr, data.Length);
					break;
				default:
					throw new Exception("Only float, double, short, int, and long are supported");
			}
			device.UnmapMemory(memory);

			return (buffer, memory);
		}

		public (Vulkan.Buffer buffer, DeviceMemory memory) CreateBuffer(DeviceSize size, BufferUsageFlags usage, MemoryPropertyFlags properties)
		{
			var bufferInfo = new BufferCreateInfo
			{
				Size = size,
				Usage = usage,
				SharingMode = SharingMode.Exclusive
			};

			var buffer = device.CreateBuffer(bufferInfo);

			var memRequirements = device.GetBufferMemoryRequirements(buffer);

			var allocInfo = new MemoryAllocateInfo
			{
				AllocationSize = memRequirements.Size,
				MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties)
			};

			var memory = device.AllocateMemory(allocInfo);
			device.BindBufferMemory(buffer, memory, 0);


			return (buffer, memory);
		}

		public void CopyBuffer(Vulkan.Buffer src, Vulkan.Buffer dst, DeviceSize size)
		{
			var commandBuffer = BeginSingleTimeCommands();

			var copyRegion = new BufferCopy
			{
				SrcOffset = 0,
				DstOffset = 0,
				Size = size
			};

			commandBuffer.CmdCopyBuffer(src, dst, copyRegion);
			EndSingleTimeCommands(commandBuffer);
		}
		#endregion

		#region Command Buffers
		protected void CreateCommandBuffers()
		{
			commandBuffers = new CommandBuffer[bufferSize];
			var allocInfo = new CommandBufferAllocateInfo
			{
				CommandPool = commandPool,
				Level = CommandBufferLevel.Primary,
				CommandBufferCount = bufferSize
			};
			commandBuffers = device.AllocateCommandBuffers(allocInfo);
			InitCommandBuffers();
		}
		public CommandBuffer BeginSingleTimeCommands()
		{
			var allocInfo = new CommandBufferAllocateInfo
			{
				Level = CommandBufferLevel.Primary,
				CommandPool = commandPool,
				CommandBufferCount = 1
			};
			var cmdBuffer = device.AllocateCommandBuffers(allocInfo).First();

			var beginInfo = new CommandBufferBeginInfo
			{
				Flags = CommandBufferUsageFlags.OneTimeSubmit
			};

			cmdBuffer.Begin(beginInfo);

			return cmdBuffer;
		}


		public void EndSingleTimeCommands(CommandBuffer commandBuffer)
		{
			commandBuffer.End();

			var submitInfo = new SubmitInfo
			{
				CommandBufferCount = 1,
				CommandBuffers = new CommandBuffer[] { commandBuffer }
			};
			graphicsQueue.Submit(submitInfo);
			graphicsQueue.WaitIdle();
			device.FreeCommandBuffer(commandPool, commandBuffer);
		}

		protected virtual void InitCommandBuffers()
		{

		}
		#endregion

		#region Semaphores and Fences
		protected void CreateSyncObjects()
		{
			imageAvailableSemaphore = new Semaphore[MAX_FRAMES_IN_FLIGHT];
			renderFinishedSemaphore = new Semaphore[MAX_FRAMES_IN_FLIGHT];
			inflightFences = new Fence[MAX_FRAMES_IN_FLIGHT];
			var sempahoreInfo = new SemaphoreCreateInfo();
			var fenceInfo = new FenceCreateInfo
			{
				Flags = FenceCreateFlags.Signaled
			};
			for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
			{
				renderFinishedSemaphore[i] = device.CreateSemaphore(sempahoreInfo);
				imageAvailableSemaphore[i] = device.CreateSemaphore(sempahoreInfo);
				inflightFences[i] = device.CreateFence(fenceInfo);
			}
		}
		#endregion

		#region Images
		public (Image image, DeviceMemory imageMemory) CreateImage(int width, int height, Format format = Format.R8G8B8A8Unorm, ImageTiling tiling = ImageTiling.Optimal, ImageUsageFlags usageFlags = ImageUsageFlags.TransferDst | ImageUsageFlags.Sampled, MemoryPropertyFlags propertyFlags = MemoryPropertyFlags.DeviceLocal)
		{
			var imageInfo = new ImageCreateInfo
			{
				ImageType = ImageType.Image2D,
				Extent = new Extent3D { Width = (uint)width, Height = (uint)height, Depth = 1 },
				MipLevels = 1,
				ArrayLayers = 1,
				Format = format,
				Tiling = tiling,
				InitialLayout = ImageLayout.Undefined,
				Usage = usageFlags,
				SharingMode = SharingMode.Exclusive,
				Samples = SampleCountFlags.Count1,
			};
			var image = device.CreateImage(imageInfo);
			var memReq = device.GetImageMemoryRequirements(image);
			var allocInfo = new MemoryAllocateInfo
			{
				AllocationSize = memReq.Size,
				MemoryTypeIndex = FindMemoryType(memReq.MemoryTypeBits, propertyFlags)
			};
			var memory = device.AllocateMemory(allocInfo);
			device.BindImageMemory(image, memory, 0);
			return (image, memory);
		}

		public void TransitionImageLayout(Image image, ImageLayout oldLayout, ImageLayout newLayout, Format format = Format.R8G8B8A8Unorm)
		{
			var cmdBuffer = BeginSingleTimeCommands();

			var barrier = new ImageMemoryBarrier
			{
				OldLayout = oldLayout,
				NewLayout = newLayout,
				SrcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED, 
				DstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
				Image = image,
				SubresourceRange = new ImageSubresourceRange
				{
					AspectMask = ImageAspectFlags.Color,
					BaseMipLevel = 0,
					LevelCount = 1,
					BaseArrayLayer = 0,
					LayerCount = 1
				},
			};

			PipelineStageFlags sourceStage = default;
			PipelineStageFlags destinationStage = default;

			if(oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
			{
				barrier.SrcAccessMask = 0;
				barrier.DstAccessMask = AccessFlags.TransferWrite;

				sourceStage = PipelineStageFlags.TopOfPipe;
				destinationStage = PipelineStageFlags.Transfer;
			}else if(oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
			{
				barrier.SrcAccessMask = AccessFlags.TransferWrite;
				barrier.DstAccessMask = AccessFlags.ShaderRead;

				sourceStage = PipelineStageFlags.Transfer;
				destinationStage = PipelineStageFlags.FragmentShader;
			}else
			{
				throw new Exception("Unsupported Transition");
			}

			cmdBuffer.CmdPipelineBarrier(sourceStage, destinationStage, 0, null, null, barrier);

			EndSingleTimeCommands(cmdBuffer);
		}

		public void CopyBufferToImage(Vulkan.Buffer buffer, Image image, int width, int height)
		{
			var cmdBuffer = BeginSingleTimeCommands();

			var region = new BufferImageCopy
			{
				BufferOffset = 0,
				BufferRowLength = 0,
				BufferImageHeight = 0,
				ImageSubresource = new ImageSubresourceLayers
				{
					AspectMask = ImageAspectFlags.Color,
					MipLevel = 0,
					BaseArrayLayer = 0,
					LayerCount = 1
				},
				ImageOffset = new Offset3D
				{
					X = 0,
					Y = 0,
					Z = 0
				},
				ImageExtent = new Extent3D
				{
					Width = (uint)width,
					Height = (uint)height,
					Depth = 1
				}
			};

			cmdBuffer.CmdCopyBufferToImage(buffer, image, ImageLayout.TransferDstOptimal, new BufferImageCopy[] { region });

			EndSingleTimeCommands(cmdBuffer);
		}
		#endregion

		public virtual void Cleanup()
		{
			device.WaitIdle();
			CleanupSwapChain();
			CleanupBuffers();
			for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
			{
				device.DestroySemaphore(imageAvailableSemaphore[i]);
				device.DestroySemaphore(renderFinishedSemaphore[i]);
				device.DestroyFence(inflightFences[i]);
			}
			device.DestroyCommandPool(commandPool);
			device.Destroy();
#if DEBUG
			data.instance.DestroyDebugReportCallbackEXT(data.debugCallback);
#endif
			data.instance.DestroySurfaceKHR(data.surface);
			data.instance.Destroy();
		}

		public virtual void CleanupBuffers()
		{

		}

#region IDisposable Support
		protected bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if(!initialized)
			{
				disposedValue = false;
				return;
			}
			if (!disposedValue)
			{
				if (disposing)
				{
					//Managed
				}
				//Unmanaged
				Cleanup();
				disposedValue = true;
			}
		}

		//Unmanaged Dispose
		~MeteoraViewBase()
		{
			Dispose(false);
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			running = false;
			Dispose(true);
			GC.SuppressFinalize(this);
		}
#endregion
	}
}