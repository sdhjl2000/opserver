﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace StackExchange.Opserver.Data.SQL
{
    public partial class SQLInstance
    {
        private Cache<List<SQLCPUEvent>> _cpuHistoryLastHour;
        public Cache<List<SQLCPUEvent>> CPUHistoryLastHour
        {
            get
            {
                return _cpuHistoryLastHour ?? (_cpuHistoryLastHour = new Cache<List<SQLCPUEvent>>
                    {
                        CacheForSeconds = 5*60,
                        UpdateCache = UpdateFromSql("CPUHistoryLastHour", conn =>
                            {
                                var sql = GetFetchSQL<SQLCPUEvent>();
                                var result = conn.Query<SQLCPUEvent>(sql, new {maxEvents = 60})
                                                 .OrderBy(e => e.EventTime)
                                                 .ToList();
                                CurrentCPUPercent = result.Count > 0 ? result.Last().ProcessUtilization : (int?) null;
                                return result;
                            })
                    });
            }
        }

        public int? CurrentCPUPercent { get; set; }

        public class SQLCPUEvent : ISQLVersionedObject
        {
            public Version MinVersion { get { return SQLServerVersions.SQL2005.RTM; } }

            public DateTime EventTime { get; internal set; }
            public int ProcessUtilization { get; internal set; }
            public int SystemIdle { get; internal set; }
            public int ExternalProcessUtilization { get { return 100 - SystemIdle - ProcessUtilization; } }

            private const string _fetchSQL = @"
Select Top (@maxEvents) 
	   DateAdd(s, (timestamp - (osi.cpu_ticks / Convert(Float, (osi.cpu_ticks / osi.ms_ticks)))) / 1000, GETUTCDATE()) AS EventTime,
	   Record.value('(./Record/SchedulerMonitorEvent/SystemHealth/SystemIdle)[1]', 'int') as SystemIdle,
	   Record.value('(./Record/SchedulerMonitorEvent/SystemHealth/ProcessUtilization)[1]', 'int') as ProcessUtilization
  From (Select timestamp, 
               convert(xml, record) As Record 
	      From sys.dm_os_ring_buffers 
		 Where ring_buffer_type = N'RING_BUFFER_SCHEDULER_MONITOR'
		   And record Like '%<SystemHealth>%') x
	    Cross Join sys.dm_os_sys_info osi
Order By timestamp Desc";

            public string GetFetchSQL(Version v)
            {
                return _fetchSQL;
            }
        }
    }
}
