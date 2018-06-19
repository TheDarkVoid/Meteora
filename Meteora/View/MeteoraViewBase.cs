﻿using System;
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

namespace Meteora.View
{
	public class MeteoraViewBase : IMeteoraView
	{
		public const uint VK_SUBPASS_INTERNAL = ~0U;
		public const int MAX_FRAMES_IN_FLIGHT = 2;

		public bool initialized;
		public bool running;
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



		#region Draw
		public virtual void DrawFrame()
		{
			if (!initialized)
				return;

			device.WaitForFence(inflightFences[currentFrame], true, ulong.MaxValue);
			device.ResetFence(inflightFences[currentFrame]);
			if(!running)
				return;

			var imageIndex = device.AcquireNextImageKHR(swapchain, ulong.MaxValue, imageAvailableSemaphore[currentFrame]);
			Semaphore[] waitSemaphoires = { imageAvailableSemaphore[currentFrame] };
			Semaphore[] signalSemaphores = { renderFinishedSemaphore[currentFrame] };
			PipelineStageFlags[] waitStages = { PipelineStageFlags.ColorAttachmentOutput };
			

			var submitInfo = new SubmitInfo
			{
				WaitSemaphoreCount = 1,
				WaitSemaphores = waitSemaphoires,
				WaitDstStageMask = waitStages,
				CommandBufferCount = 1,
				CommandBuffers = new CommandBuffer[] { commandBuffers [imageIndex] },
				SignalSemaphoreCount = 1,
				SignalSemaphores = signalSemaphores
			};

			graphicsQueue.Submit(submitInfo, inflightFences[currentFrame]);
			var swapChains = new SwapchainKhr[] { swapchain };

			var presentInfo = new PresentInfoKhr
			{
				WaitSemaphoreCount = 1,
				WaitSemaphores = signalSemaphores,
				SwapchainCount = 1,
				Swapchains = swapChains,
				ImageIndices = new uint[] { imageIndex }
			};

			presentQueue.PresentKHR(presentInfo);
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
				EnabledLayerCount = (uint)data.enabledLayers.Length,
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
				return new Extent2D
				{
					Height = Math.Max(capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height),
					Width = Math.Max(capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width)
				};
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
		protected void CreateGraphicsPipeline()
		{
			var fragModule = CreateShaderModule(File.ReadAllBytes(@"Shaders/Fragment/frag.spv"));
			var vertModule = CreateShaderModule(File.ReadAllBytes(@"Shaders/Vertex/vert.spv"));

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
				StageCount = 2,
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

			device.DestroyShaderModule(fragModule);
			device.DestroyShaderModule(vertModule);
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

			for (int i = 0; i < bufferSize; i++)
			{
				var beginInfo = new CommandBufferBeginInfo
				{
					Flags = CommandBufferUsageFlags.SimultaneousUse
				};
				commandBuffers[i].Begin(beginInfo);

				var clearColor = new ClearValue
				{
					Color = new ClearColorValue(new uint[] { 255, 0, 100, 255 })
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
				commandBuffers[i].CmdDraw(3, 1, 0, 0);
				commandBuffers[i].CmdEndRenderPass();
				commandBuffers[i].End();
			}
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
					device.WaitIdle();
					for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
					{
						device.DestroySemaphore(imageAvailableSemaphore[i]);
						device.DestroySemaphore(renderFinishedSemaphore[i]);
						device.DestroyFence(inflightFences[i]);
					}
					device.DestroyCommandPool(commandPool);
					device.DestroyPipeline(graphicsPipeline);
					device.DestroyPipelineLayout(pipelineLayout);
					device.DestroyRenderPass(renderPass);
					for (int i = 0; i < bufferSize; i++)
					{
						device.DestroyFramebuffer(framebuffers[i]);
						device.DestroyImageView(imageViews[i]);
					}
					device.DestroySwapchainKHR(swapchain);
					device.Destroy();
				}
				disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			running = false;
			Dispose(true);
			//GC.SuppressFinalize(this);
		}
		#endregion
	}
}