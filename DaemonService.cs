using System;
using System.Device.Gpio;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace PiShutdownButton
{

    public class DaemonService : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IOptions<DaemonConfig> _config;

        private GpioController controller = null;
        private const int PIN_BUTTON = 12;
        private const int PIN_LED = 16;
        private const int DELAY_PRESS = 5000;
        System.Timers.Timer timer = null;
        private bool _longPress = false;

        public DaemonService(IOptions<DaemonConfig> config)
        {
            _logger = Serilog.Log.Logger;
            _config = config;
            timer = new System.Timers.Timer(DELAY_PRESS);
            timer.Enabled = false;
            timer.Elapsed += Timer_Elapsed;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Information($"Starting daemon: {_config.Value.DaemonName}");

            StartupGpio();

            return Task.CompletedTask;
        }

        

        private void Button_Rising(object sender, PinValueChangedEventArgs args)
        {
            Button_Change(sender, args);
            // button is pressed down
            timer.Enabled = true;
        }
        private void Button_Falling(object sender, PinValueChangedEventArgs args)
        {
            Button_Change(sender, args);
            // button is released 
            timer.Enabled = false;
            if (!_longPress)
            {
                _logger.Information("Short press detected, do what?");
            }
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // button is pressed for TIMER_DELAY ms and not released yet
            _logger.Information("Long press detected, shut down");
            ShutdownGpio();
            ShutdownPi();
            _longPress = true;
        }

        private void Button_Change(object sender, PinValueChangedEventArgs args)
        {
            string activity = args.ChangeType == PinEventTypes.Rising ? "Rising" : "Falling";
            _logger.Information($"Pin {activity}");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Information("Stopping daemon.");
            ShutdownGpio();

            return Task.CompletedTask;
        }

        private void StartupGpio()
        {
            _logger.Information("Open Controller");

            if (controller == null)
                controller = new GpioController(PinNumberingScheme.Board);

            try
            {
                _logger.Information("Open LED Pin");
                controller.OpenPin(PIN_LED, PinMode.Output);

                _logger.Information("Open Button Controller");
                controller.OpenPin(PIN_BUTTON, PinMode.InputPullDown);

                _logger.Information("Write LED Pin");
                controller.Write(PIN_LED, PinValue.High);

                _logger.Information("Registering Button Rising Callback");
                controller.RegisterCallbackForPinValueChangedEvent(PIN_BUTTON, PinEventTypes.Rising, Button_Rising);

                _logger.Information("Registering Button Falling Callback");
                controller.RegisterCallbackForPinValueChangedEvent(PIN_BUTTON, PinEventTypes.Falling, Button_Falling);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while executing");
            }
            finally
            {

            }
        }

        private void ShutdownGpio()
        {
            try
            {
                _logger.Information("Close LED Pin");
                controller.ClosePin(PIN_LED);

                _logger.Information("Unregistering Button Rising Callback");
                controller.UnregisterCallbackForPinValueChangedEvent(PIN_BUTTON, Button_Rising);

                _logger.Information("Unregistering Button Falling Callback");
                controller.UnregisterCallbackForPinValueChangedEvent(PIN_BUTTON, Button_Falling);

                _logger.Information("Close Button Pin");
                controller.ClosePin(PIN_BUTTON);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while executing");
            }
            finally
            {

            }
        }

        private void ShutdownPi()
        {
            try
            {
                var process = new Process();
                process.StartInfo.FileName = "/sbin/shutdown";
                process.StartInfo.Arguments = "-h now";
                process.Start();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while executing");
            }
            finally
            {

            }
        }

        public void Dispose()
        {
            _logger.Information("Disposing....");
        }
    }
}
