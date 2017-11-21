using System;
using com.bloomberg.samples.rulemsx;
using com.bloomberg.emsx.samples;
using LogRMSX = com.bloomberg.samples.rulemsx.Log;
using LogEMSX = com.bloomberg.emsx.samples.Log;

namespace RuleMSXSingleStockHedge
{
    public class RuleMSXSingleStockHedge : NotificationHandler
    {

        private RuleMSX rmsx;
        private RuleSet ruleSet;
        private EasyMSX emsx;

        static void Main(string[] args)
        {

            System.Console.WriteLine("RuleMSX Sample - Single Stock Hedge\n");

            RuleMSXSingleStockHedge example = new RuleMSXSingleStockHedge();

            System.Console.WriteLine("Press any enter to terminate...");
            System.Console.ReadLine();
            example.Stop();

        }

        public RuleMSXSingleStockHedge()
        {
            System.Console.WriteLine("RuleMSX Starting...");

            LogRMSX.logLevel = LogRMSX.LogLevels.DETAILED;

            this.rmsx = new RuleMSX();

            this.ruleSet = rmsx.createRuleSet("SingleStockHedge");

            RuleAction actionCreateHedgeOrder = rmsx.createAction("CreateHedgeOrder", new CreateHedgeOrder("BB"));

            Rule ruleStatusWorking = new Rule("StatusWorking", new StringEqualityRule("OrderStatus", "WORKING"));
            Rule ruleFilled50Percent = new Rule("Filled50Percent", new PercentageRule(50));
            Rule ruleHedgeRequired = new Rule("HedgeRequired", new GenericBooleanRule("HedgeRequired"));

            ruleHedgeRequired.AddAction(actionCreateHedgeOrder);

            ruleSet.AddRule(ruleStatusWorking);
            ruleStatusWorking.AddRule(ruleFilled50Percent);
            ruleFilled50Percent.AddRule(ruleHedgeRequired);

            System.Console.WriteLine(ruleSet.report());

            System.Console.WriteLine("Instantiating EasyMSX...");
            LogEMSX.logLevel = LogEMSX.LogLevels.NONE;

            this.emsx = new EasyMSX(EasyMSX.Environment.BETA);
            this.emsx.orders.addNotificationHandler(this);
            this.emsx.start();

            System.Console.WriteLine("Started...");
        }

        public void Stop()
        {
            this.rmsx.Stop();
        }

        public void processNotification(Notification notification)
        {

            if (notification.category == Notification.NotificationCategory.ORDER)
            {
                if (notification.type == Notification.NotificationType.NEW || notification.type == Notification.NotificationType.INITIALPAINT)
                {
                    System.Console.WriteLine("EasyMSX Event (NEW/INITPAINT): " + notification.getOrder().field("EMSX_SEQUENCE").value());
                    parseOrder(notification.getOrder());
                }
            }
        }

        private void parseOrder(Order o)
        {

            // Create new DataSet for each order
            DataSet rmsxTest = this.rmsx.createDataSet("RMSXTest" + o.field("EMSX_SEQUENCE").value());

            // Create new data point for each required field
            DataPoint orderNo = rmsxTest.addDataPoint("OrderNo", new EMSXFieldDataPoint(o.field("EMSX_SEQUENCE")));
            DataPoint orderStatus = rmsxTest.addDataPoint("OrderStatus", new EMSXFieldDataPoint(o.field("EMSX_STATUS")));
            DataPoint amount = rmsxTest.addDataPoint("TotalAmount", new EMSXFieldDataPoint(o.field("EMSX_AMOUNT")));
            DataPoint filled = rmsxTest.addDataPoint("FilledAmount", new EMSXFieldDataPoint(o.field("EMSX_FILLED")));
            DataPoint hedgeRequired = rmsxTest.addDataPoint("HedgeRequired", new DynamicBoolDataPoint("HedgeRequired", true));

            this.ruleSet.Execute(rmsxTest);
        }

        class EMSXFieldDataPoint : DataPointSource, NotificationHandler
        {

            private Field source;

            internal EMSXFieldDataPoint(Field source)
            {
                this.source = source;
                System.Console.WriteLine("Adding field notification handler for field: " + source.name());
                source.addNotificationHandler(this);
            }

            public override object GetValue()
            {
                System.Console.WriteLine("EMSXFieldDataPoint return value for " + source.name() + ": " + source.value().ToString());
                return this.source.value().ToString();
            }

            public void processNotification(Notification notification)
            {

                // System.Console.WriteLine("Notification event: " + this.source.name() + " on " + getDataPoint().GetDataSet().getName());

                try
                {
                    System.Console.WriteLine("Category: " + notification.category.ToString() + "\tType: " + notification.type.ToString() + "\tOrder: " + notification.getOrder().field("EMSX_SEQUENCE").value());
                    foreach (FieldChange fc in notification.getFieldChanges())
                    {
                        System.Console.WriteLine("\tName: " + fc.field.name() + "\tOld: " + fc.oldValue + "\tNew: " + fc.newValue);
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine("Failed!!: " + ex.ToString());
                }

                this.SetStale();
            }

        }

        class DynamicBoolDataPoint : DataPointSource
        {

            string name;
            private volatile bool value;

            internal DynamicBoolDataPoint(string name, bool initial)
            {
                this.name = name;
                this.value = initial;
            }

            public override object GetValue()
            {
                System.Console.WriteLine("DynamicBoolDataPoint return value for " + this.name + ": " + this.value.ToString());
                return this.value;
            }

            public void setValue(bool val)
            {

                Console.WriteLine("Setting notified status to true");
                this.value = val;
                Console.WriteLine("notified status :" + this.value.ToString());
                this.SetStale();
            }
        }

        class StringEqualityRule : RuleEvaluator
        {

            string dataPointName;
            string match;

            internal StringEqualityRule(string dataPointName, string match)
            {
                this.dataPointName = dataPointName;
                this.addDependantDataPointName(dataPointName);
                this.match = match;
            }

            public override bool Evaluate(DataSet dataSet)
            {
                DataPoint dp = dataSet.getDataPoint(this.dataPointName);
                DataPointSource dps = dp.GetSource();
                string val = dps.GetValue().ToString();
                System.Console.WriteLine("StringEqualityRule dump: " + dataSet.report());
                System.Console.WriteLine("StringEqualityRule evaluating result for : " + val + " > " + match + ": result = " + (val.Equals(this.match)).ToString());
                return val.Equals(this.match);
            }

        }

        class PercentageRule : RuleEvaluator
        {

            float threshold;

            internal PercentageRule(float threshold)
            {
                this.threshold = threshold; ;
                this.addDependantDataPointName("FilledAmount");
            }

            public override bool Evaluate(DataSet dataSet)
            {
                float filled = float.Parse(dataSet.getDataPoint("FilledAmount").GetValue().ToString());
                float total = float.Parse(dataSet.getDataPoint("TotalAmount").GetValue().ToString());
                float fillPercent = (filled / total) * 100;
                System.Console.WriteLine("ExceedPercentageRule dump: " + dataSet.report());
                System.Console.WriteLine("ExceedPercentageRule evaluating result for : " + fillPercent.ToString() + " > " + threshold.ToString() + ": result = " + (fillPercent > threshold).ToString());
                return fillPercent >= threshold;
            }
        }

        class GenericBooleanRule : RuleEvaluator
        {

            string dataPointName;

            internal GenericBooleanRule(string dataPointName)
            {
                this.dataPointName = dataPointName;
                this.addDependantDataPointName(dataPointName);
            }

            public override bool Evaluate(DataSet dataSet)
            {
                String ord = (String) dataSet.getDataPoint("OrderNo").GetValue();
                bool val = bool.Parse(dataSet.getDataPoint(this.dataPointName).GetValue().ToString());
                System.Console.WriteLine("GenericBooleanRule dump: " + dataSet.report());
                System.Console.WriteLine("GenericBooleanRule evaluating result for : " + val.ToString());
                return val;
            }
        }

        class CreateHedgeOrder : ActionExecutor
        {
            string broker;

            internal CreateHedgeOrder(string broker)
            {
                this.broker = broker;
            }

            public void Execute(DataSet dataSet)
            {
                
                DynamicBoolDataPoint hedgeRequired = (DynamicBoolDataPoint)dataSet.getDataPoint("HedgeRequired").GetSource();
                hedgeRequired.setValue(false);
                System.Console.WriteLine("CreateHedgeOrder dump: " + dataSet.report());
                System.Console.WriteLine("Created order to " + broker);
                // Make CreateOrderAndRouteEx request for currency hedge.
            }
        }
    }
}
