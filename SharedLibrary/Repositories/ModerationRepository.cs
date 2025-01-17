﻿using SharedLibrary.Models;
using SharedLibrary.Data;
using SharedLibrary.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharedLibrary.Services;

namespace SharedLibrary.Repository
{
    public class ModerationRepository
    {
        private readonly FlagData _flagData;
        private readonly UserService _userService;
        private readonly PostService _postService;
        private readonly CommentService _commentService;
        private readonly LikeService _likeService;
        private List<FlagUser> _flaggedUsers;
        private List<FlagPost> _flaggedPosts;
        private List<FlagComment> _flaggedComments;

        public ModerationRepository(IServiceConfig config, UserService userService, PostService postService, CommentService commentService, LikeService likeService)
        {
            string connectionString = config.ConnectionString;
            _flagData = new FlagData(connectionString);
            _userService = userService;
            _postService = postService;
            _commentService = commentService;
            _likeService = likeService;

            LoadAllDataAsync().Wait();
        }

        private async Task LoadAllDataAsync()
        {
            _flaggedUsers = await _flagData.GetAllFlagUsers();
            foreach (var user in _flaggedUsers)
            {
                user.ToUser = _userService.GetUserById(user.ToUserId);
            }

            _flaggedPosts = await _flagData.GetAllFlagPosts();
            foreach (var post in _flaggedPosts)
            {
                post.Post = _postService.GetPostById(post.PostId);
            }

            _flaggedComments = await _flagData.GetAllFlagComments();
            foreach (var comment in _flaggedComments)
            {
                comment.Comment = _commentService.GetCommentById(comment.CommentId);
            }
        }

        public List<FlagUser> GetFlaggedUsers() => _flaggedUsers;
        public List<FlagPost> GetFlaggedPosts() => _flaggedPosts;
        public List<FlagComment> GetFlaggedComments() => _flaggedComments;

        public async Task AddFlagUser(FlagUser flagUser)
        {
            _flaggedUsers.Add(flagUser);
            await _flagData.AddFlagUser(flagUser.FromUserId, flagUser.ToUserId, flagUser.Reason);
        }

        public async Task AddFlagPost(FlagPost flagPost)
        {
            _flaggedPosts.Add(flagPost);
            await _flagData.AddFlagPost(flagPost.FromUserId, flagPost.PostId);
        }

        public async Task AddFlagComment(FlagComment flagComment)
        {
            _flaggedComments.Add(flagComment);
            await _flagData.AddFlagComment(flagComment.FromUserId, flagComment.CommentId);
        }

        public async Task RemoveFlagUser(int flagId)
        {
            var flagUser = _flaggedUsers.Find(u => u.FlagId == flagId);
            if (flagUser != null)
            {
                _flaggedUsers.Remove(flagUser);
                await _flagData.RemoveFlagUser(flagId);
            }
        }

        public async Task RemoveFlagPost(int flagId)
        {
            var flagPost = _flaggedPosts.Find(p => p.FlagId == flagId);
            if (flagPost != null)
            {
                _flaggedPosts.Remove(flagPost);
                await _flagData.RemoveFlagPost(flagId);
            }
        }

        public async Task DeleteFlaggedPost(int flagId)
        {
            var flagPost = _flaggedPosts.Find(p => p.FlagId == flagId);
            if (flagPost != null)
            {
                await _postService.DeletePostAsync(flagPost.PostId);
            }
        }

        public async Task RemoveFlagComment(int flagId)
        {
            var flagComment = _flaggedComments.Find(c => c.FlagId == flagId);
            if (flagComment != null)
            {
                _flaggedComments.Remove(flagComment);
                await _flagData.RemoveFlagComment(flagId);
            }
        }

        public async Task DeleteFlaggedComment(int flagId)
        {
            var flagComment = _flaggedComments.Find(c => c.FlagId == flagId);
            if (flagComment != null)
            {
                await _commentService.DeleteCommentAsync(flagComment.CommentId);
            }
        }


        public async Task<bool> IsAlreadyFlagged(int fromUserId, int? toUserId, int? postId, int? commentId)
        {
            return await _flagData.IsAlreadyFlagged(fromUserId, toUserId, postId, commentId);
        }

        public async Task<bool> ResolveFlag(int flagId)
        {
            var flag = (Flag)_flaggedUsers.Find(f => f.FlagId == flagId) ??
                       (Flag)_flaggedPosts.Find(p => p.FlagId == flagId) ??
                       _flaggedComments.Find(c => c.FlagId == flagId);

            if (flag != null && !flag.Resolved)
            {
                bool result = await _flagData.MarkFlagAsResolved(flagId);
                if (result)
                {
                    flag.Resolved = true;
                    return true;
                }
            }
            return false;
        }

        public int[] GetInsights()
        {
            int totalUsers = _userService.GetTotalUsers();
            int usersCreatedToday = _userService.GetUsersCreatedToday();
            int totalPosts = _postService.GetTotalPosts();
            int postsCreatedToday = _postService.GetPostsCreatedToday();
            int totalComments = _commentService.GetTotalComments();
            int totalLikes = _likeService.GetTotalLikes();

            return new int[]
            {
            totalUsers,
            usersCreatedToday,
            totalPosts,
            postsCreatedToday,
            totalComments,
            totalLikes
            };
        }


        public UserService GetUserService()
        {
            return _userService;
        }

        public CommentService GetCommentService()
        {
            return _commentService;
        }

        public PostService GetPostService()
        {
            return _postService;
        }

    }
}
