using System;
using System.Diagnostics;
using System.IO;
using ClosedXML.Excel;
using ManagedCuda;
using ManagedCuda.VectorTypes;

namespace PruebaCUDA
{
    public class Program
    {
        //INSTANCIA DE RANDOM
        static Random rand = new Random();

        //METODO MAIN
        static void Main()
        {
            //DECLARACION ITERACIONES Y N (VALORES QUE SE GENERARAN)
            int iteraciones = 10, n = 100_000_000, currentRow = 0;

            //INICIALIZACION DE LA GPU (CUDA.NET)
            var context = new CudaContext();
            var swCPU = new Stopwatch();
            var swGPU = new Stopwatch();

            //RUTA DEL ARCHIVO EXCEL
            string excelPath = @"C:\Users\Alumnos\source\repos\PruebaCUDA\Resources\Comparativa_GPUKERNEL_.xlsx", ganador = "";

            bool test;

            //BUSQUEDA DEL ARCHIVO DE EXCEL
            var workbook = File.Exists(excelPath) ? new XLWorkbook(excelPath) : new XLWorkbook();

            //BUSQUEDA DE LA TABLA
            var worksheet = workbook.Worksheets.Contains("Comparativa")
                ? workbook.Worksheet("Comparativa")
                : workbook.Worksheets.Add("Comparativa");

            //BUSQUEDA DE LA FILA Y COLUMNA
            int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow == 0)
            {
                //GENERAR ENCABEZADOS A LA HOJA DE CÁLCULO DE EXCEL
                EncabezadosExcel(worksheet);
                lastRow = 1;
            }

            //RUTA DE LA CARPETA RESOURCE
            string resourcesPath = @"C:\Users\Alumnos\source\repos\PruebaCUDA\Resources";

            //RUTAS DE ARCHIVOS
            string archivoCPU = Path.Combine(resourcesPath, "tiempoCPU.txt");
            string archivoGPU = Path.Combine(resourcesPath, "tiempoGPU.txt");

            //INICIALIZACION DE LOS ARCHIVOS PARA GUARDAR EN TXT
            StreamWriter sWGPU = new StreamWriter(archivoGPU, true);
            StreamWriter sWCPU = new StreamWriter(archivoCPU, true);

            //INICIALIZACION DE LOS VECTORES
            float[] a = new float[n];
            float[] b = new float[n];
            float[] c = new float[n];

            //INICIALIZANDO VECTORES A Y B CON NÚMEROS ALEATORIOS
            IniciandoVector(a);
            IniciandoVector(b);

            //Proceso en GPU utilizando CUDA.NET
            CudaDeviceVariable<float> d_a = new CudaDeviceVariable<float>(n);
            CudaDeviceVariable<float> d_c = new CudaDeviceVariable<float>(n);

            //COPIANDO LOS DATOS A LA GPU
            d_a.CopyToDevice(a);

            //Definición del kernel CUDA
            var kernel = context.LoadKernelPTX("SigmoideKernel.ptx", "SigmoideKernel");

            //Configuración de la grilla y el bloque
            dim3 blockSize = new dim3(512);
            dim3 gridSize = new dim3((uint)(n + blockSize.x - 1) /blockSize.x);

            //ITERACIONES PARA GENERAR LA TABLA
            for (int z = 0; z < iteraciones; z++)
            {
                //MOSTRANDO EL INICIO DE LA VUELTA
                Console.WriteLine($"Inicio vuelta {z + 1}");

                //INICIALIZACION DE VECTOR DE RESULTADOS
                Array.Clear(c, 0, c.Length);

                //DECLARACIÓN DEL STOPWATCH PARA GPU
                swGPU = Stopwatch.StartNew();

                //MOSTRANDO EN CONSOLA EL INICIO DEL CICLO DE LA GPU
                Console.WriteLine("Inicio GPU");

                //EJECUTAR EL KERNEL CUDA
                kernel.BlockDimensions = blockSize;
                kernel.GridDimensions = gridSize;
                kernel.Run(d_a.DevicePointer, d_c.DevicePointer, n);

                //LIMPIEZA DE GPU
                context.Synchronize();

                //TRANSFERIR EL RESULTADO DE LA GPU A LA CPU
                d_c.CopyToHost(c);

                //DETENER EL CRONOMETRO DE LA GPU
                swGPU.Stop();

                //FINALIZANDO CICLO DE LA GPU Y MOSTRANDO SU TIEMPO
                Console.WriteLine($"Fin GPU. Tiempo: {swGPU.ElapsedMilliseconds}");

                //DECLARACIÓN DEL STOPWATCH PARA CPU
                swCPU = Stopwatch.StartNew();
                
                //INICIANDO CICLO DE LA CPU
                Console.WriteLine("Inicio CPU");

                //INICIALIZACION DE VECTOR DE RESULTADOS
                Array.Clear(c, 0, c.Length);

                //PROCESO EN LA CPU
                for (int i = 0; i < n; i++)
                {
                    c[i] = Sigmoide(b[i]);
                }

                //DETENER EL CRONOMETRO DE LA CPU
                swCPU.Stop();

                //MOSTRANDO EL TIEMPO DE CPU EN LA VUELTA
                Console.WriteLine($"Fin CPU. Tiempo: {swCPU.ElapsedMilliseconds}");                    

                //GARBAGE COLLECTOR LIMPIANDO CPU
                GC.Collect();
                GC.WaitForPendingFinalizers();

                //ESCRITURA DE TIEMPO EN ARCHIVO TXT DE CPU Y GPU
                sWCPU.WriteLine($"{swCPU.Elapsed.TotalMilliseconds}");
                sWGPU.WriteLine($"{swGPU.Elapsed.TotalMilliseconds}");

                //ESCRITURA DE TIEMPO Y VUELTA EN ARCHIVO EXCEL
                currentRow = lastRow + z + 1;
                worksheet.Cell(currentRow, 1).Value = swCPU.Elapsed.TotalMilliseconds;
                worksheet.Cell(currentRow, 2).Value = swGPU.Elapsed.TotalMilliseconds;
                worksheet.Cell(currentRow, 3).Value = z + 1;

                //GUARDANDO EL ARCHIVO DE EXCEL
                workbook.SaveAs(excelPath);

                //ESCRIBIENDO EN CONSOLA EL NÚMERO DE VUELTA
                Console.WriteLine($"Fin de la vuelta {z + 1}\n");
            }

            //CERRANDO LOS STREAMWRITERS
            sWCPU.Close();
            sWGPU.Close();

            //ASIGNAMOS TEST FALSE SI LA CPU ES MÁS RÁPIDA QUE LA GPU Y TRUE SI LA CPU ES MÁS LENTA QUE LA GPU
            test = (swCPU.ElapsedMilliseconds < swGPU.ElapsedMilliseconds) ? false : true;

            //ASIGNAMOS EL NOMBRE DEL GANADOR
            ganador = (test) ? "GPU" : "CPU";

            //AGREGANDO LAS FÓRMULAS PARA PROMEDIAR
            worksheet.Cell(2, 5).SetFormulaA1("AVERAGE(A2:A" + currentRow + ")");
            worksheet.Cell(2, 7).SetFormulaA1("AVERAGE(B2:B" + currentRow + ")");
            worksheet.Cell(2, 9).SetFormulaA1("E2-G2");
            worksheet.Cell(2, 11).Value = ganador;
            worksheet.Cell(2, 13).SetFormulaA1((test) ? "E2/G2" : "G2/E2");

            //AJUSTANDO COLUMNAS AL CONTENIDO DE LA TABLA DE EXCEL
            worksheet.Columns().AdjustToContents();
            worksheet.CellsUsed().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.CellsUsed().Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            //GUARDANDO EL ARCHIVO DE EXCEL 
            workbook.SaveAs(excelPath);

            //CERRANDO EL ARCHIVO DE EXCEL
            workbook.Dispose();

            //CERRANDO EL USO DE LA GPU
            context.Dispose();
        }

        //METODO PARA ESCRIBIR LOS ENCABEZADOS EN LA HOJA DE EXCEL
        static void EncabezadosExcel(IXLWorksheet worksheet)
        {
            //ESCRITURA EN LAS CELDAS PARA ENCABEZADOS
            worksheet.Cell("A1").Value = "CPU (ms)";
            worksheet.Cell("B1").Value = "GPU (ms)";
            worksheet.Cell("C1").Value = "Vuelta";
            worksheet.Cell("E1").Value = "PROMEDIO CPU (ms)";
            worksheet.Cell("G1").Value = "PROMEDIO GPU (ms)";
            worksheet.Cell("I1").Value = "DIFERENCIA (ms)";
            worksheet.Cell("K1").Value = "GANÓ";
            worksheet.Cell("M1").Value = "VECES MÁS RÁPIDA";
        }

        //MÉTODO PARA INICIAR VECTOR
        static float[] IniciandoVector(float[] x)
        {
            for (int i = 0; i < x.Length; i++)
                x[i] = (float)rand.NextDouble() * 10;

            return x;
        }

        //MÉTODO DE FUNCIÓN SIGMOIDAL
        static float Sigmoide(float x) => 1.0f /(1.0f + (float)Math.Exp(-x));
    }
}
