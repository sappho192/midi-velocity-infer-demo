using Microsoft.Data.Analysis;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using mvi2demo.InferenceTools.Model;
using NumSharp;
using System.Diagnostics;

namespace mvi2demo.InferenceTools
{
    public partial class MidiVelocityInfer
    {
        private static readonly int SAMPLE_LENGTH = 4;
        private static readonly int FEATURE_NUM = 5;

        private static readonly string TOOLS_ROOT = Path.Combine(Environment.CurrentDirectory, "Tools");
        private static readonly string UTILS_PATH = Path.Combine(TOOLS_ROOT, "utils");
        private static string MODEL_PATH = Path.Combine(TOOLS_ROOT, "models",
            "mvi-v2-2023-07-20_13-00_56-h4-e5-mse_cosine_loss-alpha0.15-m0.60-LSTM-luong_attention-MAESTRO.onnx");
        private static readonly string DATASET_METADATA_PATH = Path.Combine(TOOLS_ROOT, "models",
            "dataset32-MAESTRO-len4.json");

        public bool Inference(string inputMidiFilePath, out string outputMidiFilePath)
        {
            (var dataset_metadata, var input_dataframe) = DoPreprocess(inputMidiFilePath);
            if (dataset_metadata == null || input_dataframe == null)
            {
                Console.WriteLine("Preprocess failed.");
                outputMidiFilePath = string.Empty;
                return false;
            }
            DoInference(inputMidiFilePath, dataset_metadata, input_dataframe);

            string extension = Path.GetExtension(inputMidiFilePath);
            outputMidiFilePath = $"{Path.ChangeExtension(inputMidiFilePath, null)}_predicted{extension}";
            return true;
        }


        private static void DoInference(string inputMidiFilePath, DatasetMetadata md, DataFrame input_dataframe)
        {
            // 1. Load model
            var session = new InferenceSession(MODEL_PATH);

            // 2. Prepare columns info
            List<string> columnsTrain = new()
            {
                "time_diff", "note_num", "length", "note_num_diff", "low_octave"
            };
            List<string> columnsLabel = new()
            {
                "velocity"
            };

            // 3. Convert DataFrame to NDArray
            Dictionary<string, NDArray> csvData = DataFrameToNDArray(input_dataframe);

            // 4. Make dataset from single score data
            (var demo_dataset_input, var demo_dataset_label) = MakeDataset(csvData, columnsTrain, columnsLabel, md);

            // 5. Make input and output tensors
            var input_container = new List<NamedOnnxValue>();
            NDArray inputNDArray = new NDArray(typeof(float),
                (demo_dataset_input.Count, demo_dataset_input[0].shape[0], demo_dataset_input[0].shape[1]));
            for (int i = 0; i < demo_dataset_input.Count; i++)
            {
                inputNDArray[i] = demo_dataset_input[i];
            }
            input_container.Add(NamedOnnxValue.CreateFromTensor("input_5",
                inputNDArray.ToMuliDimArray<float>().ToTensor<float>()));
            // 6. Do inference
            var result = session.Run(input_container).First();
            var resultTensor = result.AsTensor<float>().Select(
                x => Math.Clamp((int)Math.Round(x * md.velocity_max), 0, md.velocity_max)).ToList();
            var dummyCount = resultTensor.Count - csvData.First().Value.shape[0];
            for (int i = 0; i < dummyCount; i++)
            {
                resultTensor.RemoveAt(resultTensor.Count - 1);
            }

            // 7. Generate predicted result as CSV
            string outputCsvPath = ResultToCSV(csvData, resultTensor, inputMidiFilePath);

            // 8. Generate result MIDI file from result CSV
            OutputCSVtoMIDI(outputCsvPath, inputMidiFilePath);
        }

        private static void OutputCSVtoMIDI(string outputCsvPath, string inputMidiFilePath)
        {
            string csv2midiFilename;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                csv2midiFilename = Path.Combine(UTILS_PATH, "csv2midi.exe");
            }
            else
            {
                csv2midiFilename = Path.Combine(UTILS_PATH, "csv2midi");
            }

            //string extension = Path.GetExtension(inputMidiFilePath);
            //string outputMidiFilePath = $"{Path.GetFileNameWithoutExtension(inputMidiFilePath)}_predicted.{extension}";

            ProcessStartInfo startInfo = new(csv2midiFilename)
            {
                Arguments = $"{outputCsvPath} {inputMidiFilePath}",
                RedirectStandardOutput = true
            };

            using Process? process = Process.Start(startInfo);
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            Console.WriteLine(output);
            Console.WriteLine($"csv2midi exit code: {process.ExitCode}");
        }

        private static string ResultToCSV(Dictionary<string, NDArray> csvData, List<int> resultTensor, string inputMidiFilePath)
        {
            string keyTime = "time";
            string keyTimeDiff = "time_diff";
            string keyNoteNum = "note_num";
            string keyNoteNumDiff = "note_num_diff";
            string keyLength = "length";
            string keyLowOctave = "low_octave";
            string keyVelocity = "velocity";

            var dataTable = new DataFrame(new[]
            {
                new PrimitiveDataFrameColumn<int>(keyTime,          csvData[keyTime].astype(typeof(int)).ToArray<int>()),
                new PrimitiveDataFrameColumn<int>(keyTimeDiff,      csvData[keyTimeDiff].astype(typeof(int)).ToArray<int>()),
                new PrimitiveDataFrameColumn<int>(keyNoteNum,       csvData[keyNoteNum].astype(typeof(int)).ToArray<int>()),
                new PrimitiveDataFrameColumn<int>(keyNoteNumDiff,   csvData[keyNoteNumDiff].astype(typeof(int)).ToArray<int>()),
                new PrimitiveDataFrameColumn<int>(keyLength,        csvData[keyLength].astype(typeof(int)).ToArray<int>()),
                new PrimitiveDataFrameColumn<int>(keyLowOctave,     csvData[keyLowOctave].astype(typeof(int)).ToArray<int>()),
                new PrimitiveDataFrameColumn<int>(keyVelocity,      resultTensor),
            });

            string outputPath = $"{Path.ChangeExtension(inputMidiFilePath, null)}_predicted.csv";
            DataFrame.SaveCsv(dataTable, outputPath);

            return outputPath;
        }

        private static Dictionary<string, NDArray> DataFrameToNDArray(DataFrame df)
        {
            Dictionary<string, NDArray> csvData = new();
            foreach (var column in df.Columns)
            {
                // Convert each column to NDArray
                NDArray array = new NDArray(typeof(float), new Shape((int)column.Length));
                for (int i = 0; i < column.Length; i++)
                {
                    array[i] = (float)column[i];
                }
                csvData.Add(column.Name, array);
            }
            return csvData;
        }

    }
}
