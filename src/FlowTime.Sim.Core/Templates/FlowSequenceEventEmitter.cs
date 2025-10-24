using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace FlowTime.Sim.Core.Templates;

internal sealed class FlowSequenceEventEmitter : ChainedEventEmitter
{
    public FlowSequenceEventEmitter(IEventEmitter next)
        : base(next)
    {
    }

    public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
    {
        if (ShouldUseFlowStyle(eventInfo.Source.Type))
        {
            eventInfo = new SequenceStartEventInfo(eventInfo.Source)
            {
                Anchor = eventInfo.Anchor,
                Tag = eventInfo.Tag,
                Style = SequenceStyle.Flow
            };
        }

        base.Emit(eventInfo, emitter);
    }

    private static bool ShouldUseFlowStyle(Type type)
    {
        if (!type.IsArray)
        {
            return false;
        }

        var elementType = type.GetElementType();
        return elementType == typeof(double)
            || elementType == typeof(float)
            || elementType == typeof(decimal)
            || elementType == typeof(int)
            || elementType == typeof(long)
            || elementType == typeof(short);
    }
}
