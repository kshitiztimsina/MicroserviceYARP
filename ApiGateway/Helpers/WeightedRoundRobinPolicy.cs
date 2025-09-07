namespace ApiGateway.Helpers;
using System.Collections.Generic;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

public class WeightedRoundRobinPolicy : ILoadBalancingPolicy
{
    public string Name => "WeightedRoundRobin";

    private int _lastIndex = -1;



    public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
    {
        if (availableDestinations == null || availableDestinations.Count == 0)
            return null;

        // Expand destinations based on weight metadata
        var weightedList = new List<DestinationState>();
        foreach (var dest in availableDestinations)
        {
            var weight = dest.Model.Config.Metadata!.TryGetValue("weight", out var val)
                ? int.Parse(val)
                : 1;

            for (int i = 0; i < weight; i++)
            {
                weightedList.Add(dest);
            }
        }

        _lastIndex = (_lastIndex + 1) % weightedList.Count;
        return weightedList[_lastIndex];
    }
}
