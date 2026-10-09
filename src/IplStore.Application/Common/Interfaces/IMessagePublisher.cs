using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Application.Common.Interfaces
{
    public interface IMessagePublisher
    {
        Task PublishAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class;
    }
}
