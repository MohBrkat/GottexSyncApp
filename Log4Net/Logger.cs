using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Repository;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Log4NetLibrary
{
    public sealed class Logger 
    {

        bool disposed = false;

        private Logger() { }

        public static ILog GetLogger()
        {
            ILog log;
            ILoggerRepository logRepository;
            try
            {
                //get calling method type
                StackTrace stackTrace = new StackTrace();
                MethodBase methodBase = stackTrace.GetFrame(2).GetMethod();

                //get logger object
                log = LogManager.GetLogger(methodBase.DeclaringType);
                logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());

                var log4netConfigFile = new FileInfo("log4net.config");

                XmlConfigurator.Configure(logRepository, log4netConfigFile);

            }
            catch (System.Security.SecurityException)
            {
                log = LogManager.GetLogger(Assembly.GetEntryAssembly(), "[Cannot write it because of SecurityException]");
            }

            return log;


        }


        public static void LogObjectInfo(object response, object request, string message, string url = "")
        {
            ILog log = GetLogger();
            try
            {
                if (response == null)
                {
                    response = "Waiting for response";
                }

                if (log.IsDebugEnabled)
                {
                    message += $"\r URL: {url} \r Request: \r {Serialize(request)} \r Response: \r {Serialize(response)}";
                    log.Info($"\n\r {message} \n\r");
                }
            }
            catch (Exception e)
            {
                log.Error("Error while logging parameters :" + e.Message);
            }
        }

        public static string Serialize(object alert)
        {
            return JsonConvert.SerializeObject(alert);
        }

        public static ILog GetManualLogger()
        {
            ILog log;
            ILoggerRepository logRepository;
            try
            {
                //get calling method type
                StackTrace stackTrace = new StackTrace();
                MethodBase methodBase = stackTrace.GetFrame(2).GetMethod();

                //get logger object
                log = LogManager.GetLogger(methodBase.DeclaringType);
                logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());

                var log4netConfigFile = new FileInfo("log4netmanual.config");

                XmlConfigurator.Configure(logRepository, log4netConfigFile);

            }
            catch (System.Security.SecurityException)
            {
                log = LogManager.GetLogger(Assembly.GetEntryAssembly(), "[Cannot write it because of SecurityException]");
            }

            return log;


        }

        public static void StopProcess()
        {
            var fileAppender = new RollingFileAppender();
            fileAppender.ImmediateFlush = true;
            fileAppender.LockingModel = new FileAppender.MinimalLock();
            fileAppender.ActivateOptions();
        }

        public static void SetLoggerFileName(string fileName)
        {
            GlobalContext.Properties["FileName"] = fileName;
        }

        public static void EnterScope()
        {
            //get calling method
            StackTrace stackTrace = new StackTrace();
            MethodBase methodBase = stackTrace.GetFrame(1).GetMethod();

            string message = string.Format("Enter Scope : {0}", methodBase.Name);

            //get logger object
            ILog log = GetLogger();

            //write info message
            if (log.IsInfoEnabled)
            {
                log.Info(message);
            }
        }

        public static void ExitScope()
        {
            //get calling method
            StackTrace stackTrace = new StackTrace();
            MethodBase methodBase = stackTrace.GetFrame(1).GetMethod();

            string message = string.Format("Exit Scope : {0}", methodBase.Name);

            //get logger object
            ILog log = GetLogger();

            //write info message
            if (log.IsInfoEnabled)
            {
                log.Info(message);
            }
        }


        public static void LogObjectProperties(object pObject, string afterOrBefore, string operationType, string objectName)
        {
            // operationType : insert , update or delete
            // objectName : the name of the object to be displayed in logger file
            //get logger object
            ILog log = GetLogger();

            string message = string.Format("{0} {1} {2}  , {2} properties : ", afterOrBefore, operationType, objectName);

            if (pObject != null)
            {
                try
                {
                    foreach (var prop in pObject.GetType().GetProperties())
                    {
                        message += prop.Name + " = " + prop.GetValue(pObject, null) + " , ";
                    }

                    message = message.Substring(0, message.Length - 1); //remove last comma
                    log.Info(message);

                }
                catch (Exception ex)
                {
                    log.Error("Error while logging object :" + message);
                }


            }
        }
        public static void LogFunctionParameters(MethodBase method,params object[] values)
        {
            ILog log = GetLogger();
            if (method != null)
            {
                try
                {
                    ParameterInfo[] parms = method.GetParameters();
                    object[] namevalues = new object[2 * parms.Length];
                    string msg;
                    msg = "Error in " + method.Name + " Parameters number: " + parms.Length + "(";
                    for (int i = 0, j = 0; i < parms.Length; i++, j += 2)
                    {
                        msg += "{" + j + "}={" + (j + 1) + "}, ";
                        namevalues[j] = parms[i].Name;
                        if (i < values.Length) namevalues[j + 1] = values[i];
                    }
                    if(parms.Length != 0)
                    {
                        msg = msg.Substring(0, msg.Length - 2);
                    }
                    msg += ")";
                    msg = string.Format(msg, namevalues);
                    log.Info(msg);
                }
                catch (Exception ex)
                {
                    log.Error("Error while logging parameters :" + ex.Message);
                }

            }
        }
    }
}
