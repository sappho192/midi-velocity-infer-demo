using Microsoft.Data.Analysis;
using mvi2demo.InferenceTools.Model;
using NumSharp;
using System.Diagnostics;
using System.Text.Json;

namespace mvi2demo.InferenceTools
{
    public partial class MidiVelocityInfer
    {
        private static (DatasetMetadata?, DataFrame?) DoPreprocess(string inputMidiFilePath)
        {
            // 1. Generate CSV from midi files
            if (InputMIDItoCSV(inputMidiFilePath, out string inputCsvFilePath))
            {
                // 2. Read dataset metadata from JSON file
                var dataset_metadata = ReadDatasetMetadata();
                if (dataset_metadata == null) return (null, null);
                // 3. Make input dataset
                var input_dataframe = MakeInputDataframe(inputCsvFilePath);

                return (dataset_metadata, input_dataframe);
            }
            return (null, null);
        }

        private static DataFrame MakeInputDataframe(string inputCsvFilePath)
        {
            DataFrame df = DataFrame.LoadCsv(inputCsvFilePath);
            return df;
        }

        private static List<NDArray> DivideArray(NDArray array, int sampleLength, int overlappingWindow = 0)
        {
            List<NDArray> dividedArrays = new List<NDArray>();
            var transposedArray = array.T;
            var arraySize = transposedArray.shape[0];
            int i = 0;
            for (i = 0; i < arraySize - sampleLength + 1; i += sampleLength - overlappingWindow)
            {
                NDArray subArray = new NDArray(array.dtype, new Shape(sampleLength, transposedArray.shape[1]));
                for (int j = 0; j < sampleLength; j++)
                {
                    subArray[j] = transposedArray[i + j];
                }
                dividedArrays.Add(subArray);
            }
            if (arraySize % sampleLength != 0 && arraySize % sampleLength < sampleLength)
            {
                int remaining = array.size % sampleLength;
                NDArray lastSubArray = new NDArray(array.dtype, new Shape(sampleLength, transposedArray.shape[1]));
                for (int j = 0; j < remaining; j++)
                {
                    lastSubArray[j] = transposedArray[i + j];
                }
                dividedArrays.Add(lastSubArray);
            }
            return dividedArrays;
        }


        private static (List<NDArray>, List<NDArray>) MakeDataset(Dictionary<string, NDArray> csvData,
            List<string> columnsTrain, List<string> columnsLabel,
            DatasetMetadata md)
        {
            // Select the NDArrays associated with keys in columnsFirst
            NDArray[] firstNDArrayArray = columnsTrain.Select(key => csvData[key]).ToArray();
            // Stack them vertically to create the first NDArray
            NDArray dataInputRaw = np.vstack(firstNDArrayArray);

            // Select the NDArrays associated with keys in columnsSecond
            NDArray[] secondNDArrayArray = columnsLabel.Select(key => csvData[key]).ToArray();
            // Stack them vertically to create the second NDArray
            NDArray dataLabelRaw = np.vstack(secondNDArrayArray);

            //NDArray[] firstArrays = new NDArray[] { csvData[""] }

            //NDArray dataInputRaw = new NDArray(columnsTrain.Select(c => csvData[c]).ToArray());
            //NDArray dataLabelRaw = new NDArray(columnsLabel.Select(c => csvData[c]).ToArray());

            // normalize
            dataInputRaw[0] = (dataInputRaw[0] - md.train_time_diff_min) / (md.train_time_diff_max - md.train_time_diff_min);
            dataInputRaw[1] = (dataInputRaw[1] - md.note_num_min) / (md.note_num_max - md.note_num_min);
            dataInputRaw[2] = (dataInputRaw[2] - md.length_min) / (md.length_max - md.length_min);
            dataInputRaw[3] = (dataInputRaw[3] - md.note_num_diff_min) / (md.note_num_diff_max - md.note_num_diff_min);
            dataLabelRaw[0] = (dataLabelRaw[0] - md.velocity_min) / (md.velocity_max - md.velocity_min);

            var datasetInput = DivideArray(dataInputRaw, SAMPLE_LENGTH);
            //datasetInput = PadData(datasetInput, FEATURE_NUM);
            //var datasetEntireInput = np.vstack(datasetInput);

            var datasetLabel = DivideArray(dataLabelRaw, SAMPLE_LENGTH);
            //datasetLabel = PadData(datasetLabel, 1);
            //var datasetEntireLabel = np.vstack(datasetLabel);

            return (datasetInput, datasetLabel);
        }

        private static DatasetMetadata? ReadDatasetMetadata()
        {
            // Read dataset metadata from JSON file
            DatasetMetadata? dataset_metadata = null;
            // I'll use System.Text.Json instead of Newtonsoft.Json
            string dataset_metadata_json = File.ReadAllText(DATASET_METADATA_PATH);
            dataset_metadata = JsonSerializer.Deserialize<DatasetMetadata>(dataset_metadata_json);
            if (dataset_metadata == null)
            {
                Console.WriteLine("Failed to read the metadata of dataset");
                return null;
            }
            return dataset_metadata;
        }

        private static bool InputMIDItoCSV(string inputMidiFilePath, out string inputCsvFilePath)
        {
            string CURRENT_OS = Environment.OSVersion.Platform.ToString();

            // Generate csv from midi files
            string midi2csv_filename = Path.Combine(UTILS_PATH, CURRENT_OS == "Win32NT" ? "midi2csv.exe" : "midi2csv");
            ProcessStartInfo psi = new ProcessStartInfo(midi2csv_filename, inputMidiFilePath)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process? process = Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            int exit_code = process.ExitCode;

            Console.WriteLine(output);
            Console.WriteLine($"midi2csv exit code: {exit_code}");
            if (exit_code != 0)
            {
                Console.WriteLine($"midi2csv is not executed correctly (exit code: {exit_code})");
                inputCsvFilePath = string.Empty;
                return false;
            }
            inputCsvFilePath = $"{Path.ChangeExtension(inputMidiFilePath, null)}.csv";
            return true;
        }
    }
}
