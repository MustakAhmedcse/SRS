namespace SalesCom.Domain.Entities.Channels;

using SalesCom.Domain.Common;

/// <summary>A commission channel (a distribution path) identified by a unique name and a type.</summary>
public sealed class Channel : EntityBase<long>
{
    public string ChannelName { get; set; } = string.Empty;
}
