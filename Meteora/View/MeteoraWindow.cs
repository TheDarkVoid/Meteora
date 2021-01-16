using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Meteora.Data;
using SDL2;
using Vulkan;
using Vulkan.Windows;
using System.Reflection;
using System.Runtime.Serialization;

namespace Meteora.View
{
	public class MeteoraWindow : IDisposable
	{
		public const string ENGINE_NAME = "Meteora";
		public readonly uint ENGINE_VERSION = Vulkan.Version.Make(0, 0, 2);

		public InstanceCreateData data;


		private AutoResetEvent _mainLoopComplete;

		private Thread windowThread;
		private ManualResetEvent windowCreate;
		private ManualResetEvent gameInit;

		public MeteoraWindow(GameCreateInfo createInfo)
		{
			var appInfo = new ApplicationInfo
			{
				ApplicationName = createInfo.AppName,
				ApplicationVersion = Vulkan.Version.Make(0, 0, 1),
				EngineName = ENGINE_NAME,
				EngineVersion = ENGINE_VERSION,
				ApiVersion = Vulkan.Version.Make(1, 0, 0)
			};
			data.appInfo = appInfo;
			data.createInfo = createInfo;
			data.view = createInfo.View;
			_mainLoopComplete = new AutoResetEvent(false);
			windowCreate = new ManualResetEvent(false);
			gameInit = new ManualResetEvent(false);
			windowThread = new Thread(WindowLoop);
			windowThread.Start();
			windowCreate.WaitOne();
		}


		private Instance CreateInstance(ApplicationInfo appInfo)
		{
			var layerProperties = Commands.EnumerateInstanceLayerProperties();

			data.enabledDeviceExtensions = new string[] { "VK_KHR_swapchain" };

			if (SDL.SDL_Vulkan_GetInstanceExtensions(data.windowPtr, out uint count, null) == SDL.SDL_bool.SDL_FALSE)
				throw new Exception("SDL: Can't get extension count");
			var extPtr = new IntPtr[count];
			if (SDL.SDL_Vulkan_GetInstanceExtensions(data.windowPtr, out count, extPtr) == SDL.SDL_bool.SDL_FALSE)
				throw new Exception("SDL: Can't get extensions");
			var exts = extPtr.Select(ptr => Marshal.PtrToStringAnsi(ptr)).ToArray();
			//var exts = new string[] { "VK_KHR_surface", "VK_KHR_win32_surface" };

			//Debug Validation Layer
#if DEBUG
			data.enabledLayers = layerProperties.Any(l => l.LayerName == "VK_LAYER_LUNARG_standard_validation")
				? new[] { "VK_LAYER_LUNARG_standard_validation" }
				: new string[0];
#endif
			var instance = new Instance(new InstanceCreateInfo
			{
#if DEBUG
				EnabledExtensionNames = exts.Append("VK_EXT_debug_report").ToArray(),
				EnabledLayerNames = data.enabledLayers,
#else
				EnabledExtensionNames = exts,
#endif
				ApplicationInfo = appInfo
			});
#if DEBUG
			//Debug Callback
			SetupDebugCallback(instance);
#endif
			return instance;
		}

#if DEBUG
		private void SetupDebugCallback(Instance instance)
		{
			data.debugCallbackDelegate = DebugCallback;
			var createInfo = new DebugReportCallbackCreateInfoExt
			{
				Flags = DebugReportFlagsExt.Error | DebugReportFlagsExt.Warning,
				PfnCallback = Marshal.GetFunctionPointerForDelegate(data.debugCallbackDelegate)
			};
			data.debugCallback = instance.CreateDebugReportCallbackEXT(createInfo);
		}

		private Bool32 DebugCallback(DebugReportFlagsExt flags, DebugReportObjectTypeExt objectType, ulong objectHandle, IntPtr location, int messageCode, IntPtr layerPrefix, IntPtr message, IntPtr userData)
		{
			Debug.WriteLine($"[{flags} | {objectType}]: {Marshal.PtrToStringAnsi(message)} - {location}");
			Console.WriteLine($"[{flags} | {objectType}]: {Marshal.PtrToStringAnsi(message)} - {location}");
			return false;
		}
#endif

		public virtual void Init()
		{
			data.instance = CreateInstance(data.appInfo);
			var ptrProp = (typeof(IMarshalling)).GetProperty("Handle");
			var ptr = (IntPtr)ptrProp.GetValue(data.instance);
			
			SDL.SDL_Vulkan_CreateSurface(data.windowPtr, ptr, out IntPtr surface);

			data.surface = (SurfaceKhr)FormatterServices.GetSafeUninitializedObject(typeof(SurfaceKhr));
			var surfFld = typeof(SurfaceKhr).GetRuntimeFields().First();
			surfFld.SetValue(data.surface, (UInt64)surface.ToInt64());

			var devices = data.instance.EnumeratePhysicalDevices();
			if (devices.Length == 0)
				throw new Exception("No devices found");
			data.physicalDevice = devices.FirstOrDefault(IsDeviceSuitable);
			if (data.physicalDevice == null)
				throw new Exception("No Suitable Device found");
			data.view.Initialize(data);
			gameInit.Set();
		}

		private bool IsDeviceSuitable(PhysicalDevice device)
		{
			var indices = FindQueueFamilies(device);
			var swapcahinAdequate = false;
			var features = device.GetFeatures();
			var (surfaceCapablities, formats, presentModes) = QuerySwapchainSupport(device);
			if (CheckExtensionSupport(device))
				swapcahinAdequate = formats.Length != 0 && presentModes.Length != 0;
			if (indices.IsComplete && swapcahinAdequate && features.SamplerAnisotropy)
			{
				data.queueFamilyIndices = indices;
				data.surfaceCapabilities = surfaceCapablities;
				data.formats = formats;
				data.presentModes = presentModes;
				return true;
			}
			return false;
		}

		private bool CheckExtensionSupport(PhysicalDevice device)
		{
			var supportedExtensions = device.EnumerateDeviceExtensionProperties();
			return data.enabledDeviceExtensions.All(e => supportedExtensions.Any(se => se.ExtensionName == e));
		}

		private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
		{
			var queueFamilyProperties = device.GetQueueFamilyProperties();
			var queueFamilyIndices = new QueueFamilyIndices();
			for (int queueFamilyUsedIndex = 0; queueFamilyUsedIndex < queueFamilyProperties.Length; queueFamilyUsedIndex++)
			{
				//Check Present Support
				if (device.GetSurfaceSupportKHR((uint)queueFamilyUsedIndex, data.surface))
					queueFamilyIndices.PresentFamily = queueFamilyUsedIndex;
				//Check Graphics Support
				if (queueFamilyProperties[queueFamilyUsedIndex].QueueFlags.HasFlag(QueueFlags.Graphics))
					queueFamilyIndices.GraphicsFamily = queueFamilyUsedIndex;
			}
			return queueFamilyIndices;
		}

		private (SurfaceCapabilitiesKhr surfaceCapablities, SurfaceFormatKhr[] formats, PresentModeKhr[] presentModes) QuerySwapchainSupport(PhysicalDevice device)
		{
			return (device.GetSurfaceCapabilitiesKHR(data.surface), device.GetSurfaceFormatsKHR(data.surface), device.GetSurfacePresentModesKHR(data.surface));
		}

		public void OnClosing()
		{
			data.view.running = false;
			data.view.device.WaitIdle();
			_mainLoopComplete.WaitOne();
			data.view.Dispose();
		}

		public void RenderLoop()
		{
			while (data.view.running)
			{
				data.view.DrawFrame();
			}
			_mainLoopComplete.Set();
			windowThread.Join();
		}

		private void WindowLoop()
		{
			SDL.SDL_Init(SDL.SDL_INIT_AUDIO | SDL.SDL_INIT_VIDEO);
			data.windowPtr = SDL.SDL_CreateWindow(data.appInfo.ApplicationName, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, data.createInfo.Width, data.createInfo.Height,
				SDL.SDL_WindowFlags.SDL_WINDOW_VULKAN |
				SDL.SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI |
				data.createInfo.WindowFlags);
			windowCreate.Set();
			gameInit.WaitOne();
			while (data.view.running)
			{
				while (SDL.SDL_PollEvent(out var systemEvent) != 0)
				{
					switch (systemEvent.type)
					{
						case SDL.SDL_EventType.SDL_QUIT:
							data.view.running = false;
							break;
						case SDL.SDL_EventType.SDL_WINDOWEVENT:
							if (systemEvent.window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED)
							{
								data.view.render = false;
							}
							break;
					}
				}
				//SDL.SDL_SetWindowTitle(data.window, $"{data.appInfo.ApplicationName}: {data.view.FPS}fps {data.view.DeltaTime.TotalMilliseconds}ms ");
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls
		

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects).
				}
				//SDL.SDL_DestroyRenderer(_handle);
				SDL.SDL_DestroyWindow(data.windowPtr);
				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~MeteoraWindow() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion

	}
}
