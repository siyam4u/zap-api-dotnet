﻿using OWASPZAPDotNetAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;

namespace OWASPZAPDotNetAPI.Samples
{
    class AuthenticatedScanWithFormsAuthentication
    {
        private static string _target = "http://testfire.net";
        private static string _apikey = ConfigurationManager.AppSettings["zapApiKey"];
        private static int _port = Convert.ToInt32(ConfigurationManager.AppSettings["zapPort"].ToString());
        private static string _zapHost = ConfigurationManager.AppSettings["zapHost"];
        private static ClientApi _api = new ClientApi(_zapHost, _port, _apikey);
        private static IApiResponse _apiResponse;

        public static void Go()
        {
            LoadTargetUrlToSitesTree();

            string contextName = "TestFire";
            string contextId = CreateContext(contextName);
            string urlToIncludeInContext = @"http://testfire.net.*";
            string urlToExcludeInContext = @"http://testfire.net/logout.jsp";
            IncludeUrlToContext(contextName, urlToIncludeInContext);
            ExcludeUrlToContext(contextName, urlToExcludeInContext);

            if (IsFormsAuthenticationSupported())
            {
                string formBasedAuthenticationConfiguration = PrepareLoginUrlAndRequestData();
                SetFormsBasedAuthentication(contextId, formBasedAuthenticationConfiguration);
                SetLoggedInIndicatorForFormsBasedAuthentication(contextId);
            }

            string userId = string.Empty;
            string userName = "admin";
            string password = "admin";

            userId = CreateNewUser(contextId, userName);
            SetUserNameAndPassword(contextId, userId, userName, password);
            EnableUser(contextId, userId);

            SetASpecificForcedUser(contextId, userId);
            EnableForcedUserMode();

            

            string spiderScanId = StartSpidering(contextName);
            PollTheSpiderTillCompletion(spiderScanId);

            StartAjaxSpidering(contextName);
            PollTheAjaxSpiderTillCompletion();

            string activeScanId = StartActiveScanning(contextId);
            PollTheActiveScannerTillCompletion(activeScanId);

            string reportFileName = string.Format("report-{0}", DateTime.Now.ToString("dd-MMM-yyyy-hh-mm-ss"));
            WriteXmlReport(reportFileName);
            WriteHtmlReport(reportFileName);
            PrintAlertsToConsole();

            ShutdownZAP();
        }

        private static void ShutdownZAP()
        {
            _apiResponse = _api.core.shutdown();
            if ("OK" == ((ApiResponseElement)_apiResponse).Value)
                Console.WriteLine("ZAP shutdown success " + _target);
        }

        private static void PrintAlertsToConsole()
        {
            List<Alert> alerts = _api.GetAlerts(_target, 0, 0, string.Empty);
            foreach (var alert in alerts)
            {
                Console.WriteLine(alert.AlertMessage
                    + Environment.NewLine
                    + alert.CWEId
                    + Environment.NewLine
                    + alert.Url
                    + Environment.NewLine
                    + alert.WASCId
                    + Environment.NewLine
                    + alert.Evidence
                    + Environment.NewLine
                    + alert.Parameter
                    + Environment.NewLine
                );
            }
        }

        private static void WriteHtmlReport(string reportFileName)
        {
            File.WriteAllBytes(reportFileName + ".html" , _api.core.htmlreport());
        }

        private static void WriteXmlReport(string reportFileName)
        {
            File.WriteAllBytes(reportFileName + ".xml", _api.core.xmlreport());
        }

        private static void PollTheActiveScannerTillCompletion(string activeScanId)
        {
            int activeScannerprogress;
            while (true)
            {
                Sleep(5000);
                activeScannerprogress = int.Parse(((ApiResponseElement)_api.ascan.status(activeScanId)).Value);
                Console.WriteLine("Active scanner progress: {0}%", activeScannerprogress);
                if (activeScannerprogress >= 100)
                    break;
            }
            Console.WriteLine("Active scanner complete");
        }

        private static string StartActiveScanning(string contextId)
        {
            Console.WriteLine("Active Scanner: " + _target);
            _apiResponse = _api.ascan.scan(_target, "", "", "", "", "", contextId);

            string activeScanId = ((ApiResponseElement)_apiResponse).Value;
            return activeScanId;
        }

        private static void PollTheAjaxSpiderTillCompletion()
        {
            while (true)
            {
                Sleep(1000);
                string ajaxSpiderStatusText = string.Empty;
                ajaxSpiderStatusText = Convert.ToString(((ApiResponseElement)_api.ajaxspider.status()).Value);
                if (ajaxSpiderStatusText.IndexOf("running", StringComparison.InvariantCultureIgnoreCase) > -1)
                    Console.WriteLine("Ajax Spider running");
                else
                    break;
            }

            Console.WriteLine("Ajax Spider complete");
            Thread.Sleep(10000);
        }

        private static void StartAjaxSpidering(string contextName)
        {
            Console.WriteLine("Ajax Spider: " + _target);
            _apiResponse = _api.ajaxspider.scan(_target, "", contextName, "");

            if ("OK" == ((ApiResponseElement)_apiResponse).Value)
                Console.WriteLine("Ajax Spider started for " + _target);
        }

        private static void PollTheSpiderTillCompletion(string scanid)
        {
            int spiderProgress;
            while (true)
            {
                Sleep(1000);
                spiderProgress = int.Parse(((ApiResponseElement)_api.spider.status(scanid)).Value);
                Console.WriteLine("Spider progress: {0}%", spiderProgress);
                if (spiderProgress >= 100)
                    break;
            }

            Console.WriteLine("Spider complete");
            Thread.Sleep(10000);
        }

        private static string StartSpidering(string contextName)
        {
            Console.WriteLine("Spider: " + _target);
            _apiResponse = _api.spider.scan(_target, "", "", contextName, "");
            string scanid = ((ApiResponseElement)_apiResponse).Value;
            return scanid;
        }

        private static void EnableForcedUserMode()
        {
            _apiResponse = _api.forcedUser.setForcedUserModeEnabled(true);
        }

        private static void SetASpecificForcedUser(string contextId, string userId)
        {
            _apiResponse = _api.forcedUser.setForcedUser(contextId, userId);
        }

        private static void EnableUser(string contextId, string userId)
        {
            _apiResponse = _api.users.setUserEnabled(contextId, userId, "true");

            if ("OK" == ((ApiResponseElement)_apiResponse).Value)
                Console.WriteLine("user enabled");
        }

        private static void SetUserNameAndPassword(string contextId, string userId, string userName, string password)
        {
            string userAuthenticationConfigFormat = "username={0}&password={1}";
            string userAuthenticationConfig = string.Format(userAuthenticationConfigFormat, userName, password);

            _apiResponse = _api.users.setAuthenticationCredentials(contextId, userId, userAuthenticationConfig);

            if ("OK" == ((ApiResponseElement)_apiResponse).Value)
                Console.WriteLine("User credentials set");
        }

        private static string CreateNewUser(string contextId, string userName)
        {
            _apiResponse = _api.users.newUser(contextId, userName);
            string userId = ((ApiResponseElement)_apiResponse).Value;
            return userId;
        }

        private static void ConfigureFormsBasedAuthentication(string contextId)
        {

        }

        private static void SetLoggedInIndicatorForFormsBasedAuthentication(string contextId)
        {
            string loggedInIndicator = @"\Q<a id=""LoginLink"" href=""/logout.jsp"">\E";
            _apiResponse = _api.authentication.setLoggedInIndicator(contextId, loggedInIndicator);

            if ("OK" == ((ApiResponseElement)_apiResponse).Value)
                Console.WriteLine("loggedInIndicator is set");
        }

        private static void SetFormsBasedAuthentication(string contextId, string formBasedAuthenticationConfiguration)
        {
            _apiResponse = _api.authentication.setAuthenticationMethod(contextId, "formBasedAuthentication", formBasedAuthenticationConfiguration);

            if ("OK" == ((ApiResponseElement)_apiResponse).Value)
                Console.WriteLine("formBasedAuthentication is configured");
        }

        private static string PrepareLoginUrlAndRequestData()
        {
            string loginUrl = _target+"/doLogin";
            string loginRequestData = "uid={%username%}&passw={%password%}&btnSubmit=Login";
            string formBasedAuthenticationConfigurationFormat = "loginUrl={0}&loginRequestData={1}";
            string formBasedAuthenticationConfiguration = string.Format(formBasedAuthenticationConfigurationFormat,
                Uri.EscapeDataString(loginUrl),
                Uri.EscapeDataString(loginRequestData));
            return formBasedAuthenticationConfiguration;
        }

        private static bool IsFormsAuthenticationSupported()
        {
            bool isFormsAuthenticatedSupported = false;
            string formsAuthenticationString = "formBasedAuthentication";

            _apiResponse = _api.authentication.getSupportedAuthenticationMethods();

            Console.WriteLine("Supported authentication methods are:" + Environment.NewLine);
            foreach (var authenticationMethod in ((ApiResponseList)_apiResponse).List)
            {
                string authenticationMethodName = ((ApiResponseElement)authenticationMethod).Value;
                if (formsAuthenticationString.IndexOf(authenticationMethodName, StringComparison.InvariantCultureIgnoreCase) > -1)
                {
                    isFormsAuthenticatedSupported = true;
                }
                Console.WriteLine(authenticationMethodName + Environment.NewLine);
            }

            return isFormsAuthenticatedSupported;
        }

        private static void IncludeUrlToContext(string contextName, string urlToIncludeInContext)
        {
            _apiResponse = _api.context.includeInContext(contextName, urlToIncludeInContext);

            if ("OK" == ((ApiResponseElement)_apiResponse).Value)
                Console.WriteLine("{0} included to context {1}", urlToIncludeInContext, contextName);
        }

        private static void ExcludeUrlToContext(string contextName, string urlToExcludeInContext)
        {
            _apiResponse = _api.context.excludeFromContext(contextName, urlToExcludeInContext);

            if ("OK" == ((ApiResponseElement)_apiResponse).Value)
                Console.WriteLine("{0} excluded from context {1}", urlToExcludeInContext, contextName);
        }

        private static string CreateContext(string contextName)
        {
            _apiResponse = _api.context.newContext(contextName);
            string contextId = ((ApiResponseElement)_apiResponse).Value;
            Console.WriteLine("{0} context created with id {1}", contextName, contextId);
            return contextId;
        }

        private static void LoadTargetUrlToSitesTree()
        {
            _api.AccessUrl(_target);
        }

        private static void Sleep(int milliseconds)
        {
            do
            {
                Thread.Sleep(milliseconds);
                Console.WriteLine("...zz" + Environment.NewLine);
                milliseconds = milliseconds - 2000;
            } while (milliseconds > 2000);
        }
    }
}
