using System;
using Microsoft.Extensions.Logging;
using HC.Chat.Conversations;
using Microsoft.JSInterop;

namespace HC.Blazor.Pages.Chat1.Handlers
{
    /// <summary>
    /// Factory for creating chat handlers with proper dependencies
    /// ChatState is NOT registered in DI as it's component-specific
    /// </summary>
    public interface IChatHandlerFactory
    {
        IChatMessageHandler CreateMessageHandler(ChatState state);
        IChatFileHandler CreateFileHandler(ChatState state);
        IChatPaginationHandler CreatePaginationHandler(ChatState state, PaginationState pagination);
        IChatOptimizationHandler CreateOptimizationHandler(ChatState state);
    }

    /// <summary>
    /// Implementation that creates handlers with injected services
    /// </summary>
    public class ChatHandlerFactory : IChatHandlerFactory
    {
        private readonly IConversationAppService _conversationAppService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IJSRuntime _jsRuntime;

        public ChatHandlerFactory(
            IConversationAppService conversationAppService,
            ILoggerFactory loggerFactory,
            IJSRuntime jsRuntime)
        {
            _conversationAppService = conversationAppService;
            _loggerFactory = loggerFactory;
            _jsRuntime = jsRuntime;
        }

        public IChatMessageHandler CreateMessageHandler(ChatState state)
        {
            return new ChatMessageHandler(
                _conversationAppService,
                _loggerFactory.CreateLogger<ChatMessageHandler>(),
                _jsRuntime,
                state
            );
        }

        public IChatFileHandler CreateFileHandler(ChatState state)
        {
            return new ChatFileHandler(
                _conversationAppService,
                _loggerFactory.CreateLogger<ChatFileHandler>(),
                _jsRuntime,
                state
            );
        }

        public IChatPaginationHandler CreatePaginationHandler(ChatState state, PaginationState pagination)
        {
            return new ChatPaginationHandler(
                _conversationAppService,
                _loggerFactory.CreateLogger<ChatPaginationHandler>(),
                _jsRuntime,
                state,
                pagination
            );
        }

        public IChatOptimizationHandler CreateOptimizationHandler(ChatState state)
        {
            return new ChatOptimizationHandler(
                _loggerFactory.CreateLogger<ChatOptimizationHandler>(),
                state
            );
        }
    }
}
