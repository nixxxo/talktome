namespace SharedLibrary.Models
{
    public class Like
    {
        public int LikeId { get; set; }
        public int UserId { get; set; }
        public virtual User User { get; set; }
        public int PostId { get; set; }
        public virtual Post Post { get; set; }
    }
}
