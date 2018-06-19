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

namespace Meteora.View
{
	public abstract class MeteoraViewBase : IMeteoraView
	{
		public const uint VK_SUBPASS_INTERNAL = ~0U;
		public const int MAX_FRAMES_IN_FLIGHT = 2;

		public bool initialized;
		public bool running;
		public bool render = true;
		public Device device;

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
		protected Dispatcher dispatcher;

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
		private System.Windows.Forms.MethodInvoker FPSCounter;


		#region Draw
		public virtual void DrawFrame()
		{
			if (!initialized)
				return;
			if (!render)
				return;
			device.WaitForFence(inflightFences[currentFrame], true, ulong.MaxValue);
			device.ResetFence(inflightFences[currentFrame]);
			if(!running)
				return;
			if(DateTime.Now >= nextSecond )
				data.control.ParentForm.Invoke(FPSCounter);
			frameCount++;
			try
			{
				var imageIndex = device.AcquireNextImageKHR(swapchain, ulong.MaxValue, imageAvailableSemaphore[currentFrame]);
				waitSemaphores[0] = imageAvailableSemaphore[currentFrame];
				signalSemaphores[0] = renderFinishedSemaphore[currentFrame];

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
			}catch(ResultException e)
			{
				if (e.Result == Result.ErrorOutOfDateKhr || e.Result == Result.SuboptimalKhr)
				{
					if(render)
					{
						render = false;
						dispatcher.BeginInvoke(new System.Windows.Forms.MethodInvoker(RecreateSwapChain));
						device.ResetFence(inflightFences[currentFrame]);
						return;
					}
				}
				else
					throw e;
			}
			currentFrame = (currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
		}
		#endregion
		

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
				QueueCreateInfos = new DeviceQueueCreateInfo[] { graphicsQueue, presentQueue }
			};

			device = data.physicalDevice.CreateDevice(deviceInfo);
			this.graphicsQueue = device.GetQueue((uint)data.queueFamilyIndices.GraphicsFamily, 0);
			this.presentQueue = device.GetQueue((uint)data.queueFamilyIndices.PresentFamily, 0);

			CreateSwapChain();
			CreateImageViews();
			CreateRenderPass();
			CreateGraphicsPipeline();
			CreateFrameBuffers();
			CreateCommandPool();
			CreateCommandBuffers();
			CreateSyncObjects();

			FPSCounter = delegate
			{
				data.control.ParentForm.Text = $"{data.appName}: {Math.Round(frameCount / (DateTime.Now - nextSecond.AddSeconds(-1)).TotalSeconds)} FPS";
				nextSecond = DateTime.Now.AddSeconds(1);
				frameCount = 0;
			};

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

			dispatcher = Dispatcher.CurrentDispatcher;

			initialized = running = true;
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

		protected Extent2D ChooseSwapExtent(SurfaceCapabilitiesKhr capabilities)
		{
			if (capabilities.CurrentExtent.Width != uint.MaxValue)
				return capabilities.CurrentExtent;
			else
			{
				
				return new Extent2D
				{
					Height = (uint)data.control.Height,
					Width = (uint)data.control.Width
				};
			}
		}

		protected void CreateSwapChain()
		{
			format = ChooseFormat(data.formats);
			var presentMode = ChoosePresentMode(data.presentModes);
			extent = ChooseSwapExtent(data.surfaceCapabilities);

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
				var viewInfo = new ImageViewCreateInfo
				{
					Image = images[i],
					ViewType = ImageViewType.View2D,
					Format = format.Format,
					Components = new ComponentMapping
					{
						R = ComponentSwizzle.Identity,
						G = ComponentSwizzle.Identity,
						B = ComponentSwizzle.Identity,
						A = ComponentSwizzle.Identity,
					},
					SubresourceRange = new ImageSubresourceRange
					{
						AspectMask = ImageAspectFlags.Color,
						BaseMipLevel = 0,
						LevelCount = 1,
						BaseArrayLayer = 0,
						LayerCount = 1
					}
				};
				imageViews[i] = device.CreateImageView(viewInfo);
			}
		}
		#endregion

		#region Graphics Pipeline
		protected virtual void CreateGraphicsPipeline()
		{
			var shaderStages = CreateShaderStages();

			var vertexInputInfo = new PipelineVertexInputStateCreateInfo
			{
				VertexAttributeDescriptionCount = 0,
				VertexBindingDescriptionCount = 0
			};

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
				FrontFace = FrontFace.Clockwise,
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

			var layoutInfo = new PipelineLayoutCreateInfo();

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
			foreach(var stage in shaderStages)
				device.DestroyShaderModule(stage.Module);
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
			InitCommandBuffer();
		}

		protected virtual void InitCommandBuffer()
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

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					//Managed
				}
				//Unmanaged
				device.WaitIdle();
				CleanupSwapChain();
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