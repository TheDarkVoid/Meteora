using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vulkan.Windows;
using Vulkan;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Meteora.View
{
	public class MeteoraView : IDisposable
	{
		private Instance _vkInstance;
		private Device _vkDevice;
		private SwapchainKhr _vkSwapChain;
		private CommandBuffer[] _vkCommandBuffers;
		private Fence _vkFence;
		private Semaphore _vkSemaphore;
		private Queue _vkQueue;

		public MeteoraView(IntPtr hWnd)
		{
			var enabledExtensionNames = new[]
			{
				"VK_KHR_surface",
				"VK_KHR_win32_surface"
			};
			_vkInstance = new Instance(new InstanceCreateInfo
			{
				ApplicationInfo = new ApplicationInfo
				{
					EngineName = "Meteora",
					EngineVersion = 0
				},
				EnabledExtensionCount = (uint)enabledExtensionNames.Length,
				EnabledExtensionNames = enabledExtensionNames
			});
			var queueInfo = new DeviceQueueCreateInfo { QueuePriorities = new float[] { 1.0f } };
			var pDevice = _vkInstance.EnumeratePhysicalDevices()[0];
			_vkDevice = pDevice.CreateDevice(new DeviceCreateInfo
			{
				EnabledExtensionCount = 1,
				EnabledExtensionNames = new string[] { "VK_KHR_swapchain" },
				QueueCreateInfos = new DeviceQueueCreateInfo[] { queueInfo }
			});

			//TODO Figure this out
			var surface = _vkInstance.CreateWin32SurfaceKHR(new Win32SurfaceCreateInfoKhr
			{
				Hwnd = hWnd,
				Hinstance = Process.GetCurrentProcess().Handle,
				Flags = 0
			});

			_vkQueue = _vkDevice.GetQueue(0, 0);
			var sCapabilities = pDevice.GetSurfaceCapabilitiesKHR(surface);
			var sFormat = SelectFormat(pDevice, surface);

			_vkSwapChain = CreateSwapchain(surface, sCapabilities, sFormat);

			var images = _vkDevice.GetSwapchainImagesKHR(_vkSwapChain);
			var renderPass = CreateRenderPass(sFormat);
			var frameBuffers = CreateFramebuffers(images, sFormat, sCapabilities, renderPass);

			_vkCommandBuffers = CreateCommandBuffers(images, frameBuffers, renderPass, sCapabilities);
			_vkFence = _vkDevice.CreateFence(new FenceCreateInfo { });
			_vkSemaphore = _vkDevice.CreateSemaphore(new SemaphoreCreateInfo { });
			DrawFrame();
		}

		//TODO: Update draw frame

		public void DrawFrame()
		{
			uint nextIndex = _vkDevice.AcquireNextImageKHR(_vkSwapChain, ulong.MaxValue, _vkSemaphore, _vkFence);
			_vkDevice.ResetFence(_vkFence);
			var submitInfo = new SubmitInfo
			{
				WaitSemaphores = new Semaphore[] { _vkSemaphore },
				CommandBuffers = new CommandBuffer[] { _vkCommandBuffers[nextIndex] }
			};
			_vkQueue.Submit(submitInfo, _vkFence);
			_vkDevice.WaitForFence(_vkFence, true, 100000000);
			var presentInfo = new PresentInfoKhr
			{
				Swapchains = new SwapchainKhr[] { _vkSwapChain },
				ImageIndices = new uint[] { nextIndex }
			};
			_vkQueue.PresentKHR(presentInfo);
		}

		//TODO: Update Swapchain Creation

		SwapchainKhr CreateSwapchain(SurfaceKhr surface, SurfaceCapabilitiesKhr surfaceCapabilities, SurfaceFormatKhr surfaceFormat)
		{
			var swapchainInfo = new SwapchainCreateInfoKhr
			{
				Surface = surface,
				MinImageCount = surfaceCapabilities.MinImageCount,
				ImageFormat = surfaceFormat.Format,
				ImageColorSpace = surfaceFormat.ColorSpace,
				ImageExtent = surfaceCapabilities.CurrentExtent,
				ImageUsage = ImageUsageFlags.ColorAttachment,
				PreTransform = SurfaceTransformFlagsKhr.Identity,
				ImageArrayLayers = 1,
				ImageSharingMode = SharingMode.Exclusive,
				QueueFamilyIndices = new uint[] { 0 },
				PresentMode = PresentModeKhr.Fifo,
				CompositeAlpha = CompositeAlphaFlagsKhr.Inherit
			};
			return _vkDevice.CreateSwapchainKHR(swapchainInfo);
		}

		//TODO: Update format slection
		SurfaceFormatKhr SelectFormat(PhysicalDevice physicalDevice, SurfaceKhr surface)
		{
			foreach (var f in physicalDevice.GetSurfaceFormatsKHR(surface))
			{
				Console.WriteLine(f.Format);
				/*if (f.Format == Format.R8G8B8A8Unorm)
					return f;*/
			}
			return physicalDevice.GetSurfaceFormatsKHR(surface).First(x=> x.Format == Format.B8G8R8A8Srgb);

			//throw new Exception("didn't find the R8G8B8A8Unorm format");
		}


		//TODO: Update Frame buffer Creation

		Framebuffer[] CreateFramebuffers(Image[] images, SurfaceFormatKhr surfaceFormat, SurfaceCapabilitiesKhr surfaceCapabilities, RenderPass renderPass)
		{
			var displayViews = new ImageView[images.Length];
			for (int i = 0; i < images.Length; i++)
			{
				var viewCreateInfo = new ImageViewCreateInfo
				{
					Image = images[i],
					ViewType = ImageViewType.View2D,
					Format = surfaceFormat.Format,
					Components = new ComponentMapping
					{
						R = ComponentSwizzle.R,
						G = ComponentSwizzle.G,
						B = ComponentSwizzle.B,
						A = ComponentSwizzle.A
					},
					SubresourceRange = new ImageSubresourceRange
					{
						AspectMask = ImageAspectFlags.Color,
						LevelCount = 1,
						LayerCount = 1
					}
				};
				displayViews[i] = _vkDevice.CreateImageView(viewCreateInfo);
			}
			var framebuffers = new Framebuffer[images.Length];
			for (int i = 0; i < images.Length; i++)
			{
				var frameBufferCreateInfo = new FramebufferCreateInfo
				{
					Layers = 1,
					RenderPass = renderPass,
					Attachments = new ImageView[] { displayViews[i] },
					Width = surfaceCapabilities.CurrentExtent.Width,
					Height = surfaceCapabilities.CurrentExtent.Height
				};
				framebuffers[i] = _vkDevice.CreateFramebuffer(frameBufferCreateInfo);
			}
			return framebuffers;
		}

		//TODO: Update command buffer creation

		CommandBuffer[] CreateCommandBuffers(Image[] images, Framebuffer[] framebuffers, RenderPass renderPass, SurfaceCapabilitiesKhr surfaceCapabilities)
		{
			var createPoolInfo = new CommandPoolCreateInfo { Flags = CommandPoolCreateFlags.ResetCommandBuffer };
			var commandPool = _vkDevice.CreateCommandPool(createPoolInfo);
			var commandBufferAllocateInfo = new CommandBufferAllocateInfo
			{
				Level = CommandBufferLevel.Primary,
				CommandPool = commandPool,
				CommandBufferCount = (uint)images.Length
			};
			var buffers = _vkDevice.AllocateCommandBuffers(commandBufferAllocateInfo);
			for (int i = 0; i < images.Length; i++)
			{

				var commandBufferBeginInfo = new CommandBufferBeginInfo();
				buffers[i].Begin(commandBufferBeginInfo);
				var renderPassBeginInfo = new RenderPassBeginInfo
				{
					Framebuffer = framebuffers[i],
					RenderPass = renderPass,
					ClearValues = new ClearValue[] { new ClearValue { Color = new ClearColorValue(new float[] { 0.9f, 0.7f, 0.0f, 1.0f }) } },
					RenderArea = new Rect2D { Extent = surfaceCapabilities.CurrentExtent }
				};
				buffers[i].CmdBeginRenderPass(renderPassBeginInfo, SubpassContents.Inline);
				buffers[i].CmdEndRenderPass();
				buffers[i].End();
			}
			return buffers;
		}

		//TODO: Update Render Pass Creation

		RenderPass CreateRenderPass(SurfaceFormatKhr surfaceFormat)
		{
			var attDesc = new AttachmentDescription
			{
				Format = surfaceFormat.Format,
				Samples = SampleCountFlags.Count1,
				LoadOp = AttachmentLoadOp.Clear,
				StoreOp = AttachmentStoreOp.Store,
				StencilLoadOp = AttachmentLoadOp.DontCare,
				StencilStoreOp = AttachmentStoreOp.DontCare,
				InitialLayout = ImageLayout.ColorAttachmentOptimal,
				FinalLayout = ImageLayout.ColorAttachmentOptimal
			};
			var attRef = new AttachmentReference { Layout = ImageLayout.ColorAttachmentOptimal };
			var subpassDesc = new SubpassDescription
			{
				PipelineBindPoint = PipelineBindPoint.Graphics,
				ColorAttachments = new AttachmentReference[] { attRef }
			};
			var renderPassCreateInfo = new RenderPassCreateInfo
			{
				Attachments = new AttachmentDescription[] { attDesc },
				Subpasses = new SubpassDescription[] { subpassDesc }
			};
			return _vkDevice.CreateRenderPass(renderPassCreateInfo);
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					_vkDevice.Destroy();
					_vkInstance.Dispose();
					_vkDevice = null;
					_vkInstance = null;
				}


				disposedValue = true;
			}
		}

		~MeteoraView() {
			Dispose(false);
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}
