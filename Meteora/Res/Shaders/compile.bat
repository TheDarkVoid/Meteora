cd Fragment
%VK_SDK_PATH%/Bin32/glslangValidator.exe -V shader.frag
cd ../Vertex
%VK_SDK_PATH%/Bin32/glslangValidator.exe -V shader.vert
timeout 5