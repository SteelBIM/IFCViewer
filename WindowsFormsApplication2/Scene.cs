﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using GlmNet;
using SharpGL;
using SharpGL.Shaders;
using SharpGL.VertexBuffers;

namespace IFCViewer
{
    public class Scene
    {
        
        // 매트릭스
        private mat4 matProj;
        private mat4 matView;
        private mat4 matWorld;

        // 쉐이더 입력 데이터 인덱스
        private const uint attributeIndexPosition = 0;
        private const uint attributeIndexNormal = 1;

        // 쉐이더 객체
        private ShaderProgram shaderProgram;

        private float rads = 0.25f * (float)Math.PI;
        
        // 화면 너비/높이
        private float width = 0.0f;
        private float height = 0.0f;
        
        // 씬 중심
        private vec3 sceneCenter;

        // 조명
        struct LIGHT
        {
            public vec4 diffuse;
            public vec4 specular;
            public vec4 ambient;
            public vec3 position;
            public vec3 direction;
            public float range;
        }

        private LIGHT light1 = new LIGHT();
        private LIGHT light2 = new LIGHT();
        private LIGHT light3 = new LIGHT();

        // 팬 관련 변수
        private int prevMouseX = 0;
        private int prevMouseY = 0;
        private float prevPanTime = 0.0f;

        // 타이머
        private System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        private IFCViewerWrapper ifcParser = IFCViewerWrapper.Instance;
        private List<IFCItem> modelList = new List<IFCItem>();
        private Camera camera = Camera.Instance;



        #region 내부 함수
        private void AddModel(int startIndex)
        {
           for(var i= startIndex; i < ifcParser.ifcItemList.Count; ++i)
           {
               if(ifcParser.ifcItemList[i].noVerticesForFaces > 0)
               {
                   modelList.Add(ifcParser.ifcItemList[i]);
               }
           }
        }

        private void GetDimensions(ref GlmNet.vec3 min, ref GlmNet.vec3 max, ref bool initMinMax)
        {
            for (var i = 0; i < modelList.Count; ++i)
            {
                if (modelList[i].noVerticesForFaces != 0)
                {
                    if (initMinMax == false)
                    {
                        min.x = modelList[i].verticesForFaces[3 * 0 + 0];
                        min.y = modelList[i].verticesForFaces[3 * 0 + 1];
                        min.z = modelList[i].verticesForFaces[3 * 0 + 2];
                        max = min;

                        initMinMax = true;
                    }



                    Int64 j = 0;
                    while (j < modelList[i].noVerticesForFaces)
                    {
                        min.x = Math.Min(min.x, modelList[i].verticesForFaces[6 * j + 0]);
                        min.y = Math.Min(min.y, modelList[i].verticesForFaces[6 * j + 1]);
                        min.z = Math.Min(min.z, modelList[i].verticesForFaces[6 * j + 2]);

                        max.x = Math.Max(max.x, modelList[i].verticesForFaces[6 * j + 0]);
                        max.y = Math.Max(max.y, modelList[i].verticesForFaces[6 * j + 1]);
                        max.z = Math.Max(max.z, modelList[i].verticesForFaces[6 * j + 2]);

                        ++j;
                    }
                }
            }
        }

        private void GetFaceBufferSize(ref Int64 vBuffSize, ref Int64 iBuffSize, int startIndex)
        {
            if (startIndex != 0)
            {
                vBuffSize = modelList[startIndex - 1].vertexOffsetForFaces + modelList[startIndex - 1].noVerticesForFaces;
                iBuffSize = modelList[startIndex - 1].indexOffsetForFaces + modelList[startIndex - 1].noIndicesForFaces;
            }

            for (var i = startIndex; i < modelList.Count; ++i)
            {

                if (modelList[i].ifcID != 0 && modelList[i].noVerticesForFaces != 0 && modelList[i].noIndicesForFaces != 0)
                {
                    modelList[i].vertexOffsetForFaces = vBuffSize;
                    modelList[i].indexOffsetForFaces = iBuffSize;

                    for (var j = 0; j < modelList[i].materialList.Count; ++j)
                    {
                        modelList[i].materialList[j].indexArrayOffset += iBuffSize;
                        modelList[i].materialList[j].indexArrayOffsetSize = IntPtr.Add(IntPtr.Zero, sizeof(int) * (int)(modelList[i].materialList[j].indexArrayOffset));
                    }

                    vBuffSize += modelList[i].noVerticesForFaces;
                    iBuffSize += modelList[i].noIndicesForFaces;

                    if (i == 0) continue;

                    // 인덱스 오프셋
                    for (var j = 0; j < modelList[i].indicesForFaces.Length; ++j)
                    {
                        modelList[i].indicesForFaces[j] = modelList[i].indicesForFaces[j] + (int)modelList[i].vertexOffsetForFaces;
                    }

                }
            }
        }

        private void SetupLights(OpenGL gl)
        {

            light1.diffuse = new vec4(0.7f, 0.7f, 0.7f, 0.1f);
            light1.specular = new vec4(0.7f, 0.7f, 0.7f, 0.1f);
            light1.ambient = new vec4(0.7f, 0.7f, 0.7f, 0.1f);
            light1.position = new vec3(2.0f, 2.0f, 0.0f);
            vec3 vecDir1 = new vec3(-camera.Look.x, -camera.Look.y, -camera.Look.z);
            light1.direction = vecDir1;
            light1.range = 10.0f;


            light2.diffuse = new vec4(0.2f, 0.2f, 0.2f, 1.0f);
            light2.specular = new vec4(0.2f, 0.2f, 0.2f, 1.0f);
            light2.ambient = new vec4(0.2f, 0.2f, 0.2f, 1.0f);
            light2.position = new vec3(-1.0f, -1.0f, -0.5f);
            vec3 vecDir2 = new vec3(1.0f, 1.0f, 0.5f);
            light2.direction = glm.normalize(vecDir2);
            light2.range = 2.0f;


            light3.diffuse = new vec4(0.2f, 0.2f, 0.2f, 1.0f);
            light3.specular = new vec4(0.2f, 0.2f, 0.2f, 1.0f);
            light3.ambient = new vec4(0.2f, 0.2f, 0.2f, 1.0f);
            light3.position = new vec3(1.0f, 1.0f, 0.5f);
            vec3 vecDir3 = new vec3(-1.0f, -1.0f, -0.5f);
            light3.direction = glm.normalize(vecDir3);
            light3.range = 2.0f;

        }

        private void bindingLights(OpenGL gl)
        {
            shaderProgram.SetUniform3(gl, "dirLight[0].direction", light1.direction.x, light1.direction.y, light1.direction.z);
            shaderProgram.SetUniform3(gl, "dirLight[0].diffuse", light1.diffuse.x, light1.diffuse.y, light1.diffuse.z);
            shaderProgram.SetUniform3(gl, "dirLight[0].ambient", light1.ambient.x, light1.ambient.y, light1.ambient.z);
            shaderProgram.SetUniform3(gl, "dirLight[0].specular", light1.specular.x, light1.specular.y, light1.specular.z);
            shaderProgram.SetUniform3(gl, "dirLight[1].direction", light2.direction.x, light2.direction.y, light2.direction.z);
            shaderProgram.SetUniform3(gl, "dirLight[1].diffuse", light2.diffuse.x, light2.diffuse.y, light2.diffuse.z);
            shaderProgram.SetUniform3(gl, "dirLight[1].ambient", light2.ambient.x, light2.ambient.y, light2.ambient.z);
            shaderProgram.SetUniform3(gl, "dirLight[1].specular", light2.specular.x, light2.specular.y, light2.specular.z);
            shaderProgram.SetUniform3(gl, "dirLight[2].direction", light3.direction.x, light3.direction.y, light3.direction.z);
            shaderProgram.SetUniform3(gl, "dirLight[2].diffuse", light3.diffuse.x, light3.diffuse.y, light3.diffuse.z);
            shaderProgram.SetUniform3(gl, "dirLight[2].ambient", light3.ambient.x, light3.ambient.y, light3.ambient.z);
            shaderProgram.SetUniform3(gl, "dirLight[2].specular", light3.specular.x, light3.specular.y, light3.specular.z);
        }

        private void bindingMaterials(OpenGL gl, Material material)
        {
            shaderProgram.SetUniform3(gl, "material.ambient", material.ambient.x, material.ambient.y, material.ambient.z);
            shaderProgram.SetUniform3(gl, "material.diffuse", material.diffuse.x, material.diffuse.y, material.diffuse.z);
            shaderProgram.SetUniform3(gl, "material.specular", material.specular.x, material.specular.y, material.specular.z);
            shaderProgram.SetUniform3(gl, "material.emissive", material.emissive.x, material.emissive.y, material.emissive.z);
            shaderProgram.SetUniform1(gl, "material.transparency", material.transparency);
        }
        #endregion

        #region 파일 처리 함수
        public void ClearScene(OpenGL gl)
        {
            modelList.Clear();
            ifcParser.ClearMemory();
            ifcParser.vertexBuffer.Unbind(gl);
            ifcParser.indexBuffer.Unbind(gl);
            ifcParser.vertexBufferArray.Unbind(gl);

        }

        public void ParseIFCFile(string sPath, OpenGL gl)
        {
            modelList.Clear();           
            ifcParser.ParseIfcFile(sPath);
            AddModel(0);
            InitDeviceBuffer(gl, width, height, 0);
        }

        public void AppendIFCFile(string sPath, OpenGL gl)
        {
            int itemStartIndex = ifcParser.ifcItemList.Count;
            int modelStartIndex = modelList.Count;
            ifcParser.AppendFile(sPath);
            AddModel(itemStartIndex);
            InitDeviceBuffer(gl, width, height, modelStartIndex);
        }
        #endregion

        #region 렌더링 함수
        public void InitScene(OpenGL gl, float w, float h)
        {

            // 배경 클리어
            gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            // 쉐이더 프로그램 생성
            var vertexShaderSource = ShaderLoader.LoadShaderFile("Shader.vert");
            var fragmentShaderSource = ShaderLoader.LoadShaderFile("Shader.frag");
            shaderProgram = new ShaderProgram();
            shaderProgram.Create(gl, vertexShaderSource, fragmentShaderSource, null);
            shaderProgram.BindAttributeLocation(gl, attributeIndexPosition, "inputPosition");
            shaderProgram.BindAttributeLocation(gl, attributeIndexNormal, "inputNormal");

            shaderProgram.AssertValid(gl);
            
            // 원근 투영 매트릭스 생성
            rads = 0.25f * (float)Math.PI;
            matProj =camera.Perspective(rads, w / h, 1.0f, 10000.0f);
            width = w;
            height = h;


            // 시야 행렬 생성
            matView = camera.LookAt(new vec3(0.0f, 5.0f, 0.0f), new vec3(0.0f, 0.0f, 0.0f), new vec3(0.0f, 0.0f, 1.0f));

            // 버퍼 생성
            ifcParser.vertexBufferArray.Create(gl);
            ifcParser.vertexBuffer.Create(gl);
            ifcParser.indexBuffer.Create(gl);

            // 조명 설정
            SetupLights(gl);

            sw.Reset();
            sw.Start();

        }

        public void Resize(float w, float h)
        {
            width = w;
            height = h;

            matProj = camera.Perspective(rads, width/height , camera.NearDepth, camera.FarDepth);
        }

        #region 테스트 코드
        //int[] indices = {
        //                    0, 1, 2,
        //                };
        //int[] indices2 = {
        //                    2, 3, 0,
        //                 };

        //private void testCreateBuffer(OpenGL gl)
        //{
        //    // 버텍스 배열
        //    float[] vertices = {//     위치        ||      노말
        //                           0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
        //                           1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
        //                           1.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f,
        //                       };

        //    float[] vertices2 = {//     위치        ||      노말
        //                            0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 1.0f,
        //                        };



        //    // 인덱스 배열
           



            
        //    // 버텍스 버퍼 배열 객체 생성
        //    ifcParser.vertexBufferArray = new VertexBufferArray();
        //    ifcParser.vertexBufferArray.Create(gl);
        //    ifcParser.vertexBufferArray.Bind(gl);

        //    //  버텍스 버퍼 생성
            
        //    var vertexDataBuffer = new VertexBuffer();
        //    vertexDataBuffer.Create(gl);
        //    vertexDataBuffer.Bind(gl);

        //    if (true)
        //    {
        //        IntPtr vertexPointer1 = GCHandle.Alloc(vertices, GCHandleType.Pinned).AddrOfPinnedObject();
        //        IntPtr vertexPointer2 = GCHandle.Alloc(vertices2, GCHandleType.Pinned).AddrOfPinnedObject();
        //        gl.BufferData(OpenGL.GL_ARRAY_BUFFER, sizeof(float) * (vertices.Length + vertices2.Length), IntPtr.Zero, OpenGL.GL_STATIC_DRAW);
        //        gl.BufferSubData(OpenGL.GL_ARRAY_BUFFER, 0, sizeof(float) * vertices.Length, vertexPointer1);
        //        gl.BufferSubData(OpenGL.GL_ARRAY_BUFFER, sizeof(float) * vertices.Length, sizeof(float) * vertices2.Length, vertexPointer2);
        //    }

        //    gl.VertexAttribPointer(attributeIndexPosition, 3, OpenGL.GL_FLOAT, false, sizeof(float) * 6, IntPtr.Zero);
        //    gl.EnableVertexAttribArray(attributeIndexPosition);
        //    gl.VertexAttribPointer(attributeIndexNormal, 3, OpenGL.GL_FLOAT, false, sizeof(float) * 6, IntPtr.Add(IntPtr.Zero, sizeof(float) * 3));
        //    gl.EnableVertexAttribArray(attributeIndexNormal);

        //    // 인덱스 버퍼 생성
           
        //    var indexDataBuffer = new IndexBuffer();
        //    indexDataBuffer.Create(gl);
        //    indexDataBuffer.Bind(gl);

        //    if (true)
        //    {
        //        IntPtr indexPointer1 = GCHandle.Alloc(indices, GCHandleType.Pinned).AddrOfPinnedObject();
        //        IntPtr indexPointer2 = GCHandle.Alloc(indices2, GCHandleType.Pinned).AddrOfPinnedObject();
        //        gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, sizeof(int) * (indices.Length + indices2.Length), IntPtr.Zero, OpenGL.GL_STATIC_DRAW);
        //        gl.BufferSubData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0, sizeof(int) * indices.Length, indexPointer1);
        //        gl.BufferSubData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, sizeof(int) * indices.Length, sizeof(int) * indices2.Length, indexPointer2);
        //    }

        //    ifcParser.vertexBufferArray.Unbind(gl);

        //}
        #endregion

        public void InitDeviceBuffer(OpenGL gl, float width, float height, int startIndex)
        {
            #region 초점 거리 계산
            vec3 min = new vec3();
            vec3 max = new vec3();

            bool initMinMax = false;
            GetDimensions(ref min, ref max, ref initMinMax);

            sceneCenter = new vec3((max.x + min.x) / 2f, (max.y + min.y) / 2f, (max.z + min.z) / 2f);

            camera.Center = sceneCenter;

            float size = max.x - min.x;

            if (size < max.y - min.y) size = max.y - min.y;
            if (size < max.z - min.z) size = max.z - min.z;

            float thetaY = 0.25f * (float)Math.PI;
            float thetaX = 2.0f * (float)Math.Atan(width / height * (float)Math.Tan((double)thetaY * 0.5));

            // 정면부와 후면부에서 바라볼 때 x좌표를 기준으로 초점 거리를 구한다. 
            float Dx1 = camera.CalculateDistance(camera.Center.x, max.x, min.x, thetaX);
            // 측면부에서 바라볼 때 x좌표를 기준으로 초점거리를 구한다.
            float Dx2 = camera.CalculateDistance(camera.Center.z, max.z, min.z, thetaX);
            // 정면부와 후면부에서 바라볼 때 z좌표를 기준으로 초점거리를 구한다.
            float Dy1 = camera.CalculateDistance(camera.Center.z, max.z, min.z, thetaY);
            // 윗면부에서 바라볼 때 y좌표를 기준으로 초점거리를 구한다. 
            float Dy2 = camera.CalculateDistance(camera.Center.y, max.y, min.y, thetaY);

            // 가장 큰 거리를 구한다.
            float Dx = Dx1 > Dx2 ? Dx1 : Dx2;
            float Dy = Dy1 > Dy2 ? Dy1 : Dy2;

            // 카메라의 최종 초점 거리
            float cameraDistance = Dx > Dy ? Dx : Dy;
            cameraDistance *= 1.5f;
            camera.CameraDistance = cameraDistance;

            matView = camera.LookAt(new vec3(camera.Center.x, camera.Center.y - cameraDistance, camera.Center.z), 
                      camera.Center, new vec3(0.0f, 0.0f, 1.0f));
            
            // 프러스텀의 최대 깊이를 구한다.
            float maxZ = 10.0f;

            while(cameraDistance * 2.0f > maxZ)
            {
                maxZ *= 10.0f;
            }

            maxZ *= 100.0f;

            camera.FarDepth = maxZ;
            camera.NearDepth = 0.000001f * camera.FarDepth;
            camera.NearDepth = camera.NearDepth < 1.0f ? 1.0f : camera.NearDepth;

            matProj = camera.Perspective(rads, width / height, camera.NearDepth, camera.FarDepth);
            #endregion

            #region 버퍼 생성
            Int64 vBuffSize = 0, iBuffSize = 0;

            GetFaceBufferSize(ref vBuffSize, ref iBuffSize, startIndex);

            if (vBuffSize == 0)
                return;

            int vSize = 0;
            int iSize = 0;
            ifcParser.vertexBufferArray.Bind(gl);

            ifcParser.vertexBuffer.Bind(gl);
            gl.BufferData(OpenGL.GL_ARRAY_BUFFER, sizeof(float) * (int)vBuffSize * 6, IntPtr.Zero, OpenGL.GL_STATIC_DRAW);

            // 모든 객체의 버텍스 집합을 한 버퍼에 집어 넣는다.
            for (var i = 0; i < modelList.Count; ++i)
            {
                GCHandle pinnedVertexArray = GCHandle.Alloc(modelList[i].verticesForFaces, GCHandleType.Pinned);
                IntPtr vertexPointer = pinnedVertexArray.AddrOfPinnedObject();
                gl.BufferSubData(OpenGL.GL_ARRAY_BUFFER, vSize, sizeof(float) * (int)modelList[i].verticesForFaces.Length, vertexPointer);
                vSize += sizeof(float) * (int)modelList[i].verticesForFaces.Length;
                pinnedVertexArray.Free();
            }
            
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, sizeof(float) * 6, IntPtr.Zero);
            gl.EnableVertexAttribArray(attributeIndexPosition);
            gl.VertexAttribPointer(1, 3, OpenGL.GL_FLOAT, false, sizeof(float) * 6, IntPtr.Add(IntPtr.Zero, sizeof(float) * 3));
            gl.EnableVertexAttribArray(attributeIndexNormal);

            ifcParser.indexBuffer.Bind(gl);
            gl.BufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, sizeof(int) * (int)iBuffSize, IntPtr.Zero, OpenGL.GL_STATIC_DRAW);

            // 모든 객체의 인덱스 집합을 한 버퍼에 집어 넣는다.
            for (var i = 0; i < modelList.Count; ++i)
            {
                GCHandle pinnedIndexArray = GCHandle.Alloc(modelList[i].indicesForFaces, GCHandleType.Pinned);
                IntPtr indexPointer = pinnedIndexArray.AddrOfPinnedObject();
                gl.BufferSubData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, iSize, sizeof(int) * (int)modelList[i].indicesForFaces.Length, indexPointer);
                iSize += sizeof(int) * (int)modelList[i].indicesForFaces.Length;
                pinnedIndexArray.Free();
            }

            ifcParser.vertexBufferArray.Unbind(gl);
            #endregion
        }

        public void Update()
        {
            camera.UpdateViewMatrix();
            matView = camera.MatView;
        }

        public void Render(OpenGL gl)
        {
            // 장면 클리어
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT | OpenGL.GL_STENCIL_BUFFER_BIT);

            gl.Enable(OpenGL.GL_CULL_FACE);
            gl.FrontFace(OpenGL.GL_CW);

            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LEQUAL);

            //gl.Enable(OpenGL.GL_BLEND);
            //gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            //gl.Enable(OpenGL.GL_LINE_SMOOTH);

            shaderProgram.Bind(gl);
            shaderProgram.SetUniformMatrix4(gl, "matProj", matProj.to_array());
            shaderProgram.SetUniformMatrix4(gl, "matView", matView.to_array());
            bindingLights(gl);
             
           
            if (modelList.Count != 0)
            {
                ifcParser.vertexBufferArray.Bind(gl);

                // 불투명 객체
                for (var i = 0; i < modelList.Count; ++i)
                {
                    for (var k = 0; k < modelList[i].materialList.Count; ++k)
                    {
                        if (modelList[i].materialList[k].transparency > 0.9999f)
                        {
                            bindingMaterials(gl, modelList[i].materialList[k]);
                            gl.DrawElements(OpenGL.GL_TRIANGLES, (int)modelList[i].materialList[k].indexArrayCount, OpenGL.GL_UNSIGNED_INT, modelList[i].materialList[k].indexArrayOffsetSize);

                        }
                    }
                }

                gl.Enable(OpenGL.GL_BLEND);
                gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

                // 투명 객체
                for (var i = 0; i < modelList.Count; ++i)
                {
                    for (var k = 0; k < modelList[i].materialList.Count; ++k)
                    {
                        if (modelList[i].materialList[k].transparency < 0.9999f)
                        {
                            bindingMaterials(gl, modelList[i].materialList[k]);
                            gl.DrawElements(OpenGL.GL_TRIANGLES, (int)modelList[i].materialList[k].indexArrayCount, OpenGL.GL_UNSIGNED_INT, modelList[i].materialList[k].indexArrayOffsetSize);
                        }
                    }
                }
                gl.Disable(OpenGL.GL_BLEND);
                ifcParser.vertexBufferArray.Unbind(gl);
            }

            shaderProgram.Unbind(gl);

        }

        void drawFace(OpenGL gl)
        {
            for (var i = 0; i < modelList.Count; ++i)
            {
                for (var k = 0; k < modelList[i].materialList.Count; ++k)
                {
                    if (modelList[i].materialList[k].transparency > 0.9999f)
                    {
                        bindingMaterials(gl, modelList[i].materialList[k]);
                        gl.DrawElements(OpenGL.GL_TRIANGLES, (int)modelList[i].materialList[k].indexArrayCount, OpenGL.GL_UNSIGNED_INT, modelList[i].materialList[k].indexArrayOffsetSize);

                    }
                }
            }
        }

        #endregion

        #region 카메라 제어 함수 
        public void Zoom(int delta)
        {
            float d = 0.0f;
            if (delta > 0) d = -1.0f;
            if (delta < 0) d = 1.0f;

            camera.Walk(d);
        }

        public void Pan(int x, int y)
        {
            // 팬기능이 이전 보다 훨씬 시간이 지나서 활성화되면 이전 마우스 위치를 초기화 시킨다.
            // 갑자기 너무 많이 이동하는 것을 방지한다.
            float currTime = sw.ElapsedMilliseconds / 1000.0f;
            if(currTime - prevPanTime > 0.1f)
            {
                prevMouseX = x;
                prevMouseY = y;
            }

            // 변위
            float distX = (float)(x - prevMouseX) * 275.0f / height;
            float distY = (float)(y - prevMouseY) * 275.0f / height;

            camera.Strafe(distX);
            camera.Zump(distY);

            prevMouseX = x;
            prevMouseY = y;

            prevPanTime = currTime;
        }

        public void FreeOrbit(int x, int y)
        {
            float currTime = sw.ElapsedMilliseconds / 1000.0f;
            if (currTime - prevPanTime > 0.1f)
            {
                prevMouseX = x;
                prevMouseY = y;
            }

            // 변위
            float distX = -0.005f * (float)(x - prevMouseX) * 0.9f;
            float distY = -0.005f * (float)(y - prevMouseY) * 0.9f;

            camera.OrbitUp(distX);
            camera.OrbitSide(distY);

            prevMouseX = x;
            prevMouseY = y;

            prevPanTime = currTime; 

        }
        #endregion
    }
}
