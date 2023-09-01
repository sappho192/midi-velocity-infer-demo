namespace mvi2demo.InferenceTools.Model
{
    public class DatasetMetadata
    {
        public int train_time_diff_min { get; set; }
        public int train_time_diff_max { get; set; }
        public int note_num_min { get; set; }
        public int note_num_max { get; set; }
        public int note_num_diff_min { get; set; }
        public int note_num_diff_max { get; set; }
        public int length_min { get; set; }
        public int length_max { get; set; }
        public int velocity_min { get; set; }
        public int velocity_max { get; set; }
    }
}
