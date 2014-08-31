﻿using System.Collections.Generic;

namespace StackExchange.Opserver.Data.Redis
{
    public class RedisIssues : IIssuesProvider
    {
        public IEnumerable<Issue> GetIssues()
        {
            foreach (var i in RedisInstance.AllInstances.WithIssues())
            {
                yield return new Issue<RedisInstance>(i, i.Name);
            }
        }
    }
}
