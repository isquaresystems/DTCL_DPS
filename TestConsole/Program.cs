using System;
using System.Threading.Tasks;
using DTCL.Transport;
using DTCL.Cartridges;
using DTCL.Log;
using IspProtocol;
using System.IO.Ports;
using System.Linq;
using System.Collections.Generic;

namespace TestConsole
{
    /// <summary>
    /// Interactive test console for DTCL/DPS operations - bypasses all GUI complexity
    /// Supports: DTCL (2 slots), DPS2 4IN1 (4 slots), DPS3 4IN1 (4 slots)
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("    DTCL Interactive Test Console     ");
            Console.WriteLine("═══════════════════════════════════════\n");
            Log.SetLogLevel(LogLevel.Error);
            try
            {
                // Step 1: Select COM port
                string selectedPort = SelectComPort();
                if (selectedPort == null)
                {
                    Console.WriteLine("❌ No COM port selected.");
                    WaitForExit();
                    return;
                }

                // Step 2: Initialize hardware connection and detect board type
                var tester = new SimpleTester(selectedPort);
                if (!await tester.InitializeAsync())
                {
                    Console.WriteLine("❌ Failed to initialize hardware");
                    WaitForExit();
                    return;
                }

                // Step 3: Show board info
                tester.PrintBoardInfo();

                // Step 4: Interactive menu loop (adapts based on board type)
                await RunInteractiveMenuAsync(tester);

                // Cleanup
                tester.Cleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ FATAL ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
            }

            WaitForExit();
        }

        static string SelectComPort()
        {
            Console.WriteLine("📡 Scanning for available COM ports...\n");

            var ports = SerialPort.GetPortNames();

            if (ports.Length == 0)
            {
                Console.WriteLine("⚠️  No COM ports found!");
                return null;
            }

            Console.WriteLine("Available COM Ports:");
            for (int i = 0; i < ports.Length; i++)
            {
                Console.WriteLine($"  [{i + 1}] {ports[i]}");
            }

            Console.Write($"\nSelect port (1-{ports.Length}) or press Enter to use {ports[0]}: ");
            string input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine($"✓ Using default: {ports[0]}\n");
                return ports[0];
            }

            if (int.TryParse(input, out int selection) && selection >= 1 && selection <= ports.Length)
            {
                string selected = ports[selection - 1];
                Console.WriteLine($"✓ Selected: {selected}\n");
                return selected;
            }

            Console.WriteLine("⚠️  Invalid selection!");
            return null;
        }

        static async Task RunInteractiveMenuAsync(SimpleTester tester)
        {
            bool running = true;

            while (running)
            {
                Console.WriteLine("\n═══════════════════════════════════════");
                Console.WriteLine("           MAIN MENU                    ");
                Console.WriteLine("═══════════════════════════════════════");

                // Show menu options based on board type
                if (tester.BoardType == "DTCL" || tester.BoardType == "DPS2_4_IN_1")
                {
                    Console.WriteLine("[1] Darin-2 (NAND Flash) Operations");
                }

                if (tester.BoardType == "DTCL" || tester.BoardType == "DPS3_4_IN_1")
                {
                    Console.WriteLine("[2] Darin-3 (Compact Flash) Operations");
                }

                Console.WriteLine("[3] Run Stress Test");
                Console.WriteLine("[0] Exit");
                Console.WriteLine("═══════════════════════════════════════");
                Console.Write("Select option: ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        if (tester.BoardType == "DTCL" || tester.BoardType == "DPS2_4_IN_1")
                            await ShowDarin2MenuAsync(tester);
                        else
                            Console.WriteLine("⚠️  Darin-2 not available for this board.");
                        break;
                    case "2":
                        if (tester.BoardType == "DTCL" || tester.BoardType == "DPS3_4_IN_1")
                            await ShowDarin3MenuAsync(tester);
                        else
                            Console.WriteLine("⚠️  Darin-3 not available for this board.");
                        break;
                    case "3":
                        await ShowStressTestMenuAsync(tester);
                        break;
                    case "0":
                        running = false;
                        Console.WriteLine("\n👋 Exiting...");
                        break;
                    default:
                        Console.WriteLine("⚠️  Invalid option. Try again.");
                        break;
                }
            }
        }

        static async Task ShowDarin2MenuAsync(SimpleTester tester)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("      DARIN-2 (NAND Flash) OPERATIONS   ");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("[1] Erase");
            Console.WriteLine("[2] Write");
            Console.WriteLine("[3] Read");
            Console.WriteLine("[4] Run All Tests (Erase → Write → Read)");
            Console.WriteLine("[0] Back to Main Menu");
            Console.WriteLine("═══════════════════════════════════════");
            Console.Write("Select operation: ");

            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await ExecuteOperationWithSlotSelectionAsync(tester, "d2erase", "Darin-2 Erase");
                    break;
                case "2":
                    await ExecuteOperationWithSlotSelectionAsync(tester, "d2write", "Darin-2 Write");
                    break;
                case "3":
                    await ExecuteOperationWithSlotSelectionAsync(tester, "d2read", "Darin-2 Read");
                    break;
                case "4":
                    await RunAllTestsWithSlotSelectionAsync(tester, new[] { "d2erase", "d2write", "d2read" },
                        new[] { "Erase", "Write", "Read" }, "Darin-2");
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("⚠️  Invalid option.");
                    break;
            }
        }

        static async Task ShowDarin3MenuAsync(SimpleTester tester)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("    DARIN-3 (Compact Flash) OPERATIONS  ");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("[1] Format");
            Console.WriteLine("[2] Erase");
            Console.WriteLine("[3] Write");
            Console.WriteLine("[4] Read");
            Console.WriteLine("[5] Run All Tests (Format → Erase → Write → Read)");
            Console.WriteLine("[0] Back to Main Menu");
            Console.WriteLine("═══════════════════════════════════════");
            Console.Write("Select operation: ");

            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await ExecuteOperationWithSlotSelectionAsync(tester, "format", "Darin-3 Format");
                    break;
                case "2":
                    await ExecuteOperationWithSlotSelectionAsync(tester, "erase", "Darin-3 Erase");
                    break;
                case "3":
                    await ExecuteOperationWithSlotSelectionAsync(tester, "write", "Darin-3 Write");
                    break;
                case "4":
                    await ExecuteOperationWithSlotSelectionAsync(tester, "read", "Darin-3 Read");
                    break;
                case "5":
                    await RunAllTestsWithSlotSelectionAsync(tester, new[] { "format", "erase", "write", "read" },
                        new[] { "Format", "Erase", "Write", "Read" }, "Darin-3");
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("⚠️  Invalid option.");
                    break;
            }
        }

        static async Task ShowStressTestMenuAsync(SimpleTester tester)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("           STRESS TEST                  ");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("Select cartridge type:");

            if (tester.BoardType == "DTCL" || tester.BoardType == "DPS2_4_IN_1")
                Console.WriteLine("[1] Darin-2 (NAND Flash)");

            if (tester.BoardType == "DTCL" || tester.BoardType == "DPS3_4_IN_1")
                Console.WriteLine("[2] Darin-3 (Compact Flash)");

            Console.WriteLine("[0] Back to Main Menu");
            Console.WriteLine("═══════════════════════════════════════");
            Console.Write("Select option: ");

            string cartChoice = Console.ReadLine();

            if (cartChoice == "0") return;

            string operationType = "";
            switch (cartChoice)
            {
                case "1":
                    if (tester.BoardType != "DTCL" && tester.BoardType != "DPS2_4_IN_1")
                    {
                        Console.WriteLine("⚠️  Darin-2 not available for this board.");
                        return;
                    }
                    Console.Write("\nSelect D2 operation (d2erase/d2write/d2read): ");
                    operationType = Console.ReadLine()?.ToLower();
                    break;
                case "2":
                    if (tester.BoardType != "DTCL" && tester.BoardType != "DPS3_4_IN_1")
                    {
                        Console.WriteLine("⚠️  Darin-3 not available for this board.");
                        return;
                    }
                    Console.Write("\nSelect D3 operation (format/erase/write/read): ");
                    operationType = Console.ReadLine()?.ToLower();
                    break;
                default:
                    Console.WriteLine("⚠️  Invalid option.");
                    return;
            }

            // Get slot(s) - automatic for DTCL, user selection for DPS (only detected slots shown)
            var selectedSlots = await GetSlotsForOperationAsync(tester);
            if (selectedSlots.Count == 0)
            {
                Console.WriteLine("⚠️  No slots selected.");
                return;
            }

            Console.Write("Enter number of iterations (default 100): ");
            string iterInput = Console.ReadLine();
            int iterations = string.IsNullOrWhiteSpace(iterInput) ? 100 : int.Parse(iterInput);

            // Run stress test on selected slots
            foreach (byte slot in selectedSlots)
            {
                Console.WriteLine($"\n🚀 Starting stress test on Slot {slot}: {iterations} iterations of {operationType}...\n");
                var results = await tester.RunTestAsync(operationType, iterations, slot);
                PrintResults(results, slot);
            }
        }

        static async Task<List<byte>> GetSlotsForOperationAsync(SimpleTester tester)
        {
            // First, scan for detected slots
            Console.WriteLine("\n🔍 Scanning for detected carts...");
            var detectedSlots = await tester.ScanDetectedSlotsAsync();

            if (detectedSlots.Count == 0)
            {
                Console.WriteLine("⚠️  No carts detected in any slot.");
                return new List<byte>();
            }

            // Display detected slots
            Console.WriteLine($"✅ Detected {detectedSlots.Count} cart(s):");
            foreach (var kvp in detectedSlots)
            {
                Console.WriteLine($"   Slot {kvp.Key}: {kvp.Value}");
            }

            // DTCL: Automatically use all detected slots without asking
            if (tester.BoardType == "DTCL")
            {
                var slots = detectedSlots.Keys.ToList();
                Console.WriteLine($"\nℹ️  DTCL - operations will run on all detected slots");
                return slots;
            }

            // DPS: Ask user to select from detected slots
            return SelectSlots(tester, detectedSlots);
        }

        static List<byte> SelectSlots(SimpleTester tester, Dictionary<byte, string> detectedSlots)
        {
            Console.WriteLine("\n═══════════════════════════════════════");
            Console.WriteLine("         SELECT SLOT(S)                ");
            Console.WriteLine("═══════════════════════════════════════");

            // Show only detected slots
            var detectedSlotsList = detectedSlots.Keys.OrderBy(x => x).ToList();

            foreach (var slot in detectedSlotsList)
            {
                Console.WriteLine($"[{slot}] Slot {slot} ({detectedSlots[slot]})");
            }
            Console.WriteLine("[5] All Detected Slots");
            Console.WriteLine("═══════════════════════════════════════");
            Console.Write("Select slot(s) (e.g., 1 or 1,2,3 or 5 for all): ");

            string input = Console.ReadLine();
            var selectedSlots = new List<byte>();

            if (input == "5")
            {
                // All detected slots
                selectedSlots = detectedSlotsList;
                Console.WriteLine($"✓ Selected: All detected slots ({string.Join(", ", selectedSlots)})");
            }
            else
            {
                // Parse individual slots
                var parts = input.Split(',');
                foreach (var part in parts)
                {
                    if (byte.TryParse(part.Trim(), out byte slot) && detectedSlots.ContainsKey(slot))
                    {
                        if (!selectedSlots.Contains(slot))
                            selectedSlots.Add(slot);
                    }
                }

                if (selectedSlots.Count > 0)
                {
                    Console.WriteLine($"✓ Selected slots: {string.Join(", ", selectedSlots)}");
                }
                else if (parts.Length > 0)
                {
                    Console.WriteLine("⚠️  Invalid slot selection. Please choose from detected slots only.");
                }
            }

            return selectedSlots;
        }

        static async Task ExecuteOperationWithSlotSelectionAsync(SimpleTester tester, string operation, string displayName)
        {
            // Get slot(s) - automatic for DTCL, user selection for DPS (only detected slots shown)
            var selectedSlots = await GetSlotsForOperationAsync(tester);
            if (selectedSlots.Count == 0)
            {
                Console.WriteLine("⚠️  No slots selected.");
                return;
            }

            // Ask for iterations
            Console.Write($"\n🔢 Enter number of iterations for {displayName} (default 1): ");
            string iterInput = Console.ReadLine();
            int iterations = string.IsNullOrWhiteSpace(iterInput) ? 1 : int.Parse(iterInput);

            // Execute on each selected slot
            foreach (byte slot in selectedSlots)
            {
                if (iterations == 1)
                {
                    // Single execution
                    Console.WriteLine($"\n🚀 Executing {displayName} on Slot {slot}...");

                    var startTime = DateTime.Now;

                    try
                    {
                        int result = await tester.ExecuteOperationAsync(operation, slot);
                        var elapsed = DateTime.Now - startTime;

                        if (result == returnCodes.DTCL_SUCCESS)
                        {
                            Console.WriteLine($"✅ Slot {slot}: {displayName} completed successfully in {elapsed.TotalSeconds:F1}s");
                        }
                        else
                        {
                            Console.WriteLine($"❌ Slot {slot}: {displayName} failed (return code: {result}) after {elapsed.TotalSeconds:F1}s");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Slot {slot}: Exception during {displayName}: {ex.Message}");
                    }
                }
                else
                {
                    // Multiple iterations - stress test
                    Console.WriteLine($"\n🚀 Starting {iterations} iterations of {displayName} on Slot {slot}...\n");
                    var results = await tester.RunTestAsync(operation, iterations, slot);
                    PrintResults(results, slot);
                }
            }
        }

        static async Task RunAllTestsWithSlotSelectionAsync(SimpleTester tester, string[] operations, string[] names, string cartType)
        {
            // Get slot(s) - automatic for DTCL, user selection for DPS (only detected slots shown)
            var selectedSlots = await GetSlotsForOperationAsync(tester);
            if (selectedSlots.Count == 0)
            {
                Console.WriteLine("⚠️  No slots selected.");
                return;
            }

            // Ask for iterations
            Console.Write($"\n🔢 Enter number of iterations for ALL {cartType} tests (default 1): ");
            string iterInput = Console.ReadLine();
            int iterations = string.IsNullOrWhiteSpace(iterInput) ? 1 : int.Parse(iterInput);

            // Execute on each selected slot
            foreach (byte slot in selectedSlots)
            {
                Console.WriteLine($"\n═══════════════════════════════════════");
                Console.WriteLine($"  Slot {slot}: ALL {cartType} Tests ({iterations}x)");
                Console.WriteLine($"═══════════════════════════════════════");

                var overallStartTime = DateTime.Now;
                int totalTests = operations.Length * iterations;
                int passedTests = 0;
                int failedTests = 0;

                for (int iter = 1; iter <= iterations; iter++)
                {
                    if (iterations > 1)
                    {
                        Console.WriteLine($"\n--- Iteration {iter}/{iterations} ---");
                    }

                    for (int i = 0; i < operations.Length; i++)
                    {
                        string operation = operations[i];
                        string name = names[i];
                        string displayName = $"{cartType} {name}";

                        Console.Write($"[{((iter - 1) * operations.Length) + i + 1,3}/{totalTests}] {displayName,-20} ");

                        var startTime = DateTime.Now;

                        try
                        {
                            int result = await tester.ExecuteOperationAsync(operation, slot);
                            var elapsed = DateTime.Now - startTime;

                            if (result == returnCodes.DTCL_SUCCESS)
                            {
                                Console.WriteLine($"✓ PASS ({elapsed.TotalSeconds:F1}s)");
                                passedTests++;
                            }
                            else
                            {
                                Console.WriteLine($"✗ FAIL (code: {result}, {elapsed.TotalSeconds:F1}s)");
                                failedTests++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"✗ EXCEPTION: {ex.Message}");
                            failedTests++;
                        }

                        await Task.Delay(100);
                    }

                    if (iter < iterations)
                        await Task.Delay(200);
                }

                var totalElapsed = DateTime.Now - overallStartTime;

                Console.WriteLine($"\n═══════════════════════════════════════");
                Console.WriteLine($"  Slot {slot}: ALL TESTS COMPLETE");
                Console.WriteLine($"═══════════════════════════════════════");
                Console.WriteLine($"Total Tests:   {totalTests}");
                Console.WriteLine($"Passed:        {passedTests}");
                Console.WriteLine($"Failed:        {failedTests}");
                Console.WriteLine($"Success Rate:  {(passedTests * 100.0 / totalTests):F1}%");
                Console.WriteLine($"Total Time:    {totalElapsed.TotalSeconds:F1}s");
                Console.WriteLine($"═══════════════════════════════════════");

                if (passedTests == totalTests)
                {
                    Console.WriteLine($"\n✅ Slot {slot}: All tests passed!");
                }
                else
                {
                    Console.WriteLine($"\n⚠️  Slot {slot}: {failedTests} test(s) failed.");
                }
            }
        }

        static void PrintResults(TestResults results, byte slot)
        {
            Console.WriteLine($"\n═══════════════════════════════════════");
            Console.WriteLine($"    Slot {slot} Results: {results.Passed}/{results.Total} PASSED");
            Console.WriteLine($"    Success Rate: {results.SuccessRate:F1}%");
            Console.WriteLine($"    Failed: {results.Failed}");
            if (results.Exceptions > 0)
                Console.WriteLine($"    Exceptions: {results.Exceptions}");
            Console.WriteLine($"═══════════════════════════════════════");

            if (results.SuccessRate < 100)
            {
                Console.WriteLine($"\n⚠️  Slot {slot}: Some tests failed. Check logs above for details.");
            }
            else
            {
                Console.WriteLine($"\n✅ Slot {slot}: All tests passed!");
            }
        }

        static void WaitForExit()
        {
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }

    /// <summary>
    /// Simple test harness that bypasses HardwareInfo and GUI dependencies
    /// </summary>
    class SimpleTester
    {
        readonly string _comPort;
        UartIspTransport _transport = null;
        DataHandlerIsp _dataHandler = null;
        Darin3 _darin3 = null;
        Darin2 _darin2 = null;

        public string BoardType { get; private set; } = "Unknown";

        // Default paths matching GUI configuration
        const string D3_UPLOAD_PATH = @"c:\mps\DARIN3\upload\";
        const string D3_DOWNLOAD_PATH = @"c:\mps\DARIN3\download\";
        const string D2_UPLOAD_PATH = @"c:\mps\DARIN2\upload\";
        const string D2_DOWNLOAD_PATH = @"c:\mps\DARIN2\download\";

        public SimpleTester(string comPort)
        {
            _comPort = comPort;
        }

        public async Task<bool> InitializeAsync()
        {
            Console.WriteLine("🔧 Initializing hardware...");

            try
            {
                // Step 1: Create and open transport
                Console.Write($"   Connecting to {_comPort}... ");
                _transport = new UartIspTransport(_comPort, 115200);

                try
                {
                    _transport.Open();
                    Console.WriteLine("✓ Connected");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FAILED: {ex.Message}");
                    return false;
                }

                await Task.Delay(500);

                // Step 2: Initialize data handler
                Console.Write("   Creating data handler... ");
                _dataHandler = DataHandlerIsp.Instance;
                Console.WriteLine("✓");

                // Step 3: Create cart objects
                Console.Write("   Creating cart handlers... ");
                _darin3 = new Darin3();
                _darin2 = new Darin2();
                Console.WriteLine("✓");

                // Step 4: Initialize and register handlers
                Console.Write("   Registering protocol handlers... ");
                _dataHandler.Initialize(_transport, _darin3);
                _dataHandler.RegisterSubCommandHandlers(_darin3);
                _dataHandler.RegisterSubCommandHandlers(_darin2);
                Console.WriteLine("✓");

                // Step 5: Detect board type
                Console.Write("   Detecting board type... ");
                await DetectBoardTypeAsync();
                Console.WriteLine($"✓ {BoardType}");

                Console.WriteLine("✅ Initialization complete\n");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Initialization error: {ex.Message}");
                return false;
            }
        }

        async Task DetectBoardTypeAsync()
        {
            try
            {
                // Send BOARD_ID command to detect hardware type
                var boardIdCmd = HardwareInfo.Instance.CreateIspCommand(IspSubCommand.BOARD_ID, new byte[0]);
                var response = await _dataHandler.ExecuteCMD(boardIdCmd, (int)IspSubCmdRespLen.BOARD_ID, 3000); // Increased timeout

                if (response != null)
                {
                    byte boardIdByte = response[0];

                    switch (boardIdByte)
                    {
                        case (byte)IspBoardId.DPS2_4_IN_1:
                            BoardType = "DPS2_4_IN_1";
                            break;
                        case (byte)IspBoardId.DPS3_4_IN_1:
                            BoardType = "DPS3_4_IN_1";
                            break;
                        case (byte)IspBoardId.DTCL:
                            BoardType = "DTCL";
                            break;
                        default:
                            BoardType = $"Unknown (0x{boardIdByte:X2})";
                            break;
                    }
                }
                else
                {
                    BoardType = "Detection Fail";
                }
            }
            catch
            {
                BoardType = "Detection Failed";
            }
        }

        public void PrintBoardInfo()
        {
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("         BOARD INFORMATION             ");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine($"Board Type:  {BoardType}");
            Console.WriteLine($"COM Port:    {_comPort}");
            Console.WriteLine($"Max Slots:   {GetMaxSlots()}");
            Console.WriteLine("═══════════════════════════════════════");
        }

        public async Task<Dictionary<byte, string>> ScanDetectedSlotsAsync()
        {
            var detectedSlots = new Dictionary<byte, string>();

            try
            {
                // Send CART_STATUS command to detect carts in all slots
                var cartStatusCmd = HardwareInfo.Instance.CreateIspCommand(IspSubCommand.CART_STATUS, new byte[0]);
                var response = await _dataHandler.ExecuteCMD(cartStatusCmd, (int)IspSubCmdRespLen.CART_STATUS, 2000);

                if (response != null && response.Length >= GetMaxSlots())
                {
                    int maxSlots = GetMaxSlots();

                    for (int i = 0; i < maxSlots; i++)
                    {
                        byte slotNum = (byte)(i + 1);
                        string cartType = MapResponseToCartType(response[i]);

                        if (cartType != "Unknown")
                        {
                            detectedSlots[slotNum] = cartType;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error scanning slots: {ex.Message}");
            }

            return detectedSlots;
        }

        string MapResponseToCartType(byte responseValue)
        {
            switch (responseValue)
            {
                case 1: return "Darin-1";
                case 2: return "Darin-2";
                case 3: return "Darin-3";
                default: return "Unknown";
            }
        }

        public int GetMaxSlots()
        {
            switch (BoardType)
            {
                case "DPS2_4_IN_1":
                case "DPS3_4_IN_1":
                    return 4;
                case "DTCL":
                    return 2;
                default:
                    return 4; // Default to 4 for unknown boards
            }
        }

        public async Task<TestResults> RunTestAsync(string operation, int iterations, byte cartNo)
        {
            var results = new TestResults { Total = iterations };
            var startTime = DateTime.Now;

            Console.WriteLine($"Starting {iterations} iterations of {operation.ToUpper()} on Slot {cartNo}...\n");

            for (int i = 0; i < iterations; i++)
            {
                Console.Write($"[{i + 1,4}/{iterations}] ");
                Log.Info($"Starting iteration {i + 1} of {operation} on Slot {cartNo}...");

                try
                {
                    int result = await ExecuteOperationAsync(operation, cartNo);

                    if (result == returnCodes.DTCL_SUCCESS)
                    {
                        Console.WriteLine("✓ PASS");
                        results.Passed++;
                    }
                    else
                    {
                        Console.WriteLine($"✗ FAIL (return code: {result})");
                        results.Failed++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ EXCEPTION: {ex.Message}");
                    results.Exceptions++;
                }

                if (i < iterations - 1)
                    await Task.Delay(50);

                if ((i + 1) % 10 == 0)
                {
                    var elapsed = DateTime.Now - startTime;
                    var rate = (i + 1) / elapsed.TotalSeconds;
                    Console.WriteLine($"    Progress: {results.Passed}/{i + 1} passed ({rate:F1} iter/sec)");
                }
            }

            var totalTime = DateTime.Now - startTime;
            Console.WriteLine($"\n⏱️  Total time: {totalTime.TotalSeconds:F1} seconds ({iterations / totalTime.TotalSeconds:F1} iter/sec)");

            return results;
        }

        public async Task<int> ExecuteOperationAsync(string operation, byte cartNo)
        {
            var progress = new Progress<int>();

            Func<string, string, DTCL.CustomMessageBox.MessageBoxResult> nullHandler =
                (msg, title) => DTCL.CustomMessageBox.MessageBoxResult.Yes;

            switch (operation)
            {
                // Darin3 operations
                case "write":
                    Log.Info($"Executing Darin-3 Write on Slot {cartNo}...");
                    return await _darin3.WriteUploadFiles(
                        D3_UPLOAD_PATH,
                        nullHandler,
                        cartNo,
                        progress);

                case "read":
                    Log.Info($"Executing Darin-3 Read on Slot {cartNo}...");
                    return await _darin3.ReadDownloadFiles(
                        D3_DOWNLOAD_PATH,
                        nullHandler,
                        cartNo,
                        progress,
                        checkHeaderInfo: true);

                case "erase":
                    Log.Info($"Executing Darin-3 Erase on Slot {cartNo}...");
                    return await _darin3.EraseCartFiles(
                        progress,
                        cartNo,
                        trueErase: false);

                case "format":
                    Log.Info($"Executing Darin-3 Format on Slot {cartNo}...");
                    return await _darin3.Format(
                        progress,
                        cartNo);

                // Darin2 operations
                case "d2write":
                    Log.Info($"Executing Darin-2 Write on Slot {cartNo}...");
                    return await _darin2.WriteUploadFiles(
                        D2_UPLOAD_PATH,
                        nullHandler,
                        cartNo,
                        progress);

                case "d2read":
                    Log.Info($"Executing Darin-2 Read on Slot {cartNo}...");
                    return await _darin2.ReadDownloadFiles(
                        D2_DOWNLOAD_PATH,
                        nullHandler,
                        cartNo,
                        progress,
                        checkHeaderInfo: true);

                case "d2erase":
                    Log.Info($"Executing Darin-2 Erase on Slot {cartNo}...");
                    return await _darin2.EraseCartFiles(
                        progress,
                        cartNo,
                        trueErase: false);

                default:
                    throw new ArgumentException($"Unknown operation: {operation}");
            }
        }

        public void Cleanup()
        {
            try
            {
                _transport?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Cleanup warning: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Test results container
    /// </summary>
    class TestResults
    {
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Exceptions { get; set; }
        public double SuccessRate => Total > 0 ? (Passed * 100.0 / Total) : 0;
    }
}
