﻿using System;
using System.Drawing;
using System.Linq;
using System.Web.Mvc;
using System.Web.UI.DataVisualization.Charting;
using StackExchange.Opserver.Data.Dashboard;
using StackExchange.Opserver.Data.SQL;
using StackExchange.Opserver.Helpers;
using StackExchange.Opserver.Models;

namespace StackExchange.Opserver.Controllers
{
    public partial class GraphController
    {
        private const int SparkHours = 24;

        [OutputCache(Duration = 120, VaryByParam = "id", VaryByContentEncoding = "gzip;deflate", VaryByCustom="highDPI")]
        [Route("graph/cpu/spark"), AlsoAllow(Roles.InternalRequest)]
        public ActionResult CPUSpark(int id)
        {
            var node = DashboardData.GetNodeById(id);
            if (node == null) return ContentNotFound();

            var chart = GetSparkChart();
            var dataPoints = node.GetCPUUtilization(start: DateTime.UtcNow.AddHours(-SparkHours),
                                                    end: null,
                                                    pointCount: (int) chart.Width.Value);

            var area = GetSparkChartArea(100);
            var avgCPU = GetSparkSeries("Avg Load");
            chart.Series.Add(avgCPU);

            foreach (var mp in dataPoints)
            {
                if (mp.AvgLoad.HasValue)
                    avgCPU.Points.Add(new DataPoint(mp.DateTime.ToOADate(), mp.AvgLoad.Value));
            }

            chart.ChartAreas.Add(area);

            return chart.ToResult();
        }

        [OutputCache(Duration = 120, VaryByParam = "id", VaryByContentEncoding = "gzip;deflate", VaryByCustom = "highDPI")]
        [Route("graph/memory/spark"), AlsoAllow(Roles.InternalRequest)]
        public ActionResult MemorySpark(int id)
        {
            var node = DashboardData.GetNodeById(id);
            if (node == null) return ContentNotFound();

            var chart = GetSparkChart();
            var dataPoints = node.GetMemoryUtilization(start: DateTime.UtcNow.AddHours(-SparkHours),
                                                       end: null,
                                                       pointCount: (int) chart.Width.Value).ToList();
            var maxMem = dataPoints.Max(mp => mp.TotalMemory).GetValueOrDefault();
            var maxGB = (int)Math.Ceiling(maxMem / _gb);

            var area = GetSparkChartArea(maxMem + (maxGB / 8) * _gb);
            var used = GetSparkSeries("Used");
            chart.Series.Add(used);

            foreach (var mp in dataPoints)
            {
                if (mp.AvgMemoryUsed.HasValue)
                    used.Points.Add(new DataPoint(mp.DateTime.ToOADate(), mp.AvgMemoryUsed.Value));
            }
            chart.ChartAreas.Add(area);

            return chart.ToResult();
        }

        [OutputCache(Duration = 120, VaryByParam = "id", VaryByContentEncoding = "gzip;deflate", VaryByCustom = "highDPI")]
        [Route("graph/network/spark"), AlsoAllow(Roles.InternalRequest)]
        public ActionResult NetworkSpark(int id)
        {
            var node = DashboardData.GetNodeById(id);
            if (node == null) return ContentNotFound();

            var chart = GetSparkChart();
            var dataPoints = node.PrimaryInterfaces.SelectMany(
                ni => ni.GetUtilization(start: DateTime.UtcNow.AddHours(-SparkHours),
                                        end: null,
                                        pointCount: (int) chart.Width.Value))
                                 .OrderBy(dp => dp.DateTime);

            var area = GetSparkChartArea();
            var series = GetSparkSeries("Total");
            series.ChartType = SeriesChartType.StackedArea;
            chart.Series.Add(series);

            foreach (var np in dataPoints)
            {
                series.Points.Add(new DataPoint(np.DateTime.ToOADate(), np.InAvgBps.GetValueOrDefault(0) + np.OutAvgBps.GetValueOrDefault(0)));
            }
            chart.DataManipulator.Group("SUM", 2, IntervalType.Minutes, series);

            chart.ChartAreas.Add(area);

            return chart.ToResult();
        }

        [OutputCache(Duration = 120, VaryByParam = "id", VaryByContentEncoding = "gzip;deflate", VaryByCustom = "highDPI")]
        [Route("graph/interface/{direction}/spark")]
        public ActionResult InterfaceOutSpark(string direction, int id)
        {
            var i = DashboardData.GetInterfaceById(id);
            if (i == null) return ContentNotFound();

            var chart = GetSparkChart();
            var dataPoints = i.GetUtilization(start: DateTime.UtcNow.AddHours(-SparkHours),
                                              end: null,
                                              pointCount: (int) chart.Width.Value)
                              .OrderBy(dp => dp.DateTime);

            var area = GetSparkChartArea();
            var series = GetSparkSeries("Bytes");
            chart.Series.Add(series);

            foreach (var np in dataPoints)
            {
                series.Points.Add(new DataPoint(np.DateTime.ToOADate(),
                                                direction == "out"
                                                    ? np.OutAvgBps.GetValueOrDefault(0)
                                                    : np.InAvgBps.GetValueOrDefault(0)));
            }
            chart.ChartAreas.Add(area);

            return chart.ToResult();
        }

        [OutputCache(Duration = 120, VaryByParam = "node", VaryByContentEncoding = "gzip;deflate", VaryByCustom = "highDPI")]
        [Route("graph/sql/cpu/spark")]
        public ActionResult SQLCPUSpark(string node)
        {
            var instance = SQLInstance.Get(node);
            if (instance == null) return ContentNotFound("SQLNode not found with name = '" + node + "'");

            var chart = GetSparkChart(20, 100);
            var dataPoints = instance.CPUHistoryLastHour;

            var area = GetSparkChartArea(noLine: true);
            area.AxisY.Maximum = 100;
            area.AxisX.Minimum = DateTime.UtcNow.AddHours(-1).ToOADate();
            area.AxisX.Maximum = DateTime.UtcNow.ToOADate();
            var series = GetSparkSeries("PercentCPU");
            chart.Series.Add(series);

            if (dataPoints.HasData())
            {
                foreach (var cpu in dataPoints.Data)
                {
                    series.Points.Add(new DataPoint(cpu.EventTime.ToOADate(), cpu.ProcessUtilization));
                }
            }
            chart.ChartAreas.Add(area);

            return chart.ToResult();
        }

        private static ChartArea GetSparkChartArea(double? max = null, int? daysAgo = null, bool noLine = false)
        {
            var area = new ChartArea("area")
            {
                BackColor = Color.Transparent,
                Position = new ElementPosition(0, 0, 100, 100),
                InnerPlotPosition = new ElementPosition(0, 0, 100, 100),
                AxisY =
                {
                    MaximumAutoSize = 100,
                    LabelStyle = { Enabled = false },
                    MajorGrid = { Enabled = false },
                    MajorTickMark = { Enabled = false },
                    LineColor = Color.Transparent,
                    LineDashStyle = ChartDashStyle.Dot,
                },
                AxisX =
                {
                    MaximumAutoSize = 100,
                    LabelStyle = { Enabled = false },
                    Maximum = DateTime.UtcNow.ToOADate(),
                    Minimum = DateTime.UtcNow.AddDays(-(daysAgo ?? 1)).ToOADate(),
                    MajorGrid = { Enabled = false },
                    LineColor = ColorTranslator.FromHtml("#a3c0d7")
                }
            };

            if (max.HasValue)
                area.AxisY.Maximum = max.Value;
            if (noLine)
                area.AxisX.LineColor = Color.Transparent;

            return area;
        }

        private static Series GetSparkSeries(string name, Color? color = null)
        {
            color = color ?? Color.SteelBlue;
            return new Series(name)
                       {
                           ChartType = SeriesChartType.Area,
                           XValueType = ChartValueType.DateTime,
                           Color = ColorTranslator.FromHtml("#c6d5e2"),
                           EmptyPointStyle = { Color = Color.Transparent, BackSecondaryColor = Color.Transparent }
                       };
        }
    }
}