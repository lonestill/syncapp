package com.ecosystem.android.ui

import android.Manifest
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import com.ecosystem.android.model.Device
import com.ecosystem.android.model.DeviceStatus
import com.ecosystem.android.network.EcosystemService
import com.ecosystem.android.ui.theme.DeviceEcosystemTheme
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions

class MainActivity : ComponentActivity() {
    private var devices by mutableStateOf(listOf<Device>())
    private var isConnected by mutableStateOf(false)
    private var connectedDevice by mutableStateOf<Device?>(null)
    private var statusMessage by mutableStateOf("")

    private val deviceMap = mutableMapOf<String, Device>()

    private val requestPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { isGranted: Boolean ->
        if (isGranted) {
            startEcosystemService()
        }
    }

    private val qrScannerLauncher = registerForActivityResult(ScanContract()) { result ->
        if (result.contents != null) {
            handleQrScan(result.contents)
        }
    }

    private val serviceReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            when (intent?.action) {
                EcosystemService.ACTION_DEVICE_DISCOVERED -> {
                    val ip = intent.getStringExtra(EcosystemService.EXTRA_DEVICE_IP) ?: return
                    val port = intent.getIntExtra(EcosystemService.EXTRA_DEVICE_PORT, 55556)
                    val name = intent.getStringExtra(EcosystemService.EXTRA_DEVICE_NAME) ?: "Unknown"
                    addOrUpdateDevice(Device(name, ip, port, "Windows", DeviceStatus.DISCOVERED))
                }
                EcosystemService.ACTION_DEVICE_LOST -> {
                    val ip = intent.getStringExtra(EcosystemService.EXTRA_DEVICE_IP) ?: return
                    deviceMap.remove(ip)
                    refreshDeviceList()
                }
                EcosystemService.ACTION_CONNECTION_STATUS_CHANGED -> {
                    val connected = intent.getBooleanExtra(EcosystemService.EXTRA_CONNECTED, false)
                    if (connected) {
                        val ip = intent.getStringExtra(EcosystemService.EXTRA_DEVICE_IP) ?: ""
                        val name = intent.getStringExtra(EcosystemService.EXTRA_DEVICE_NAME) ?: ""
                        val port = intent.getIntExtra(EcosystemService.EXTRA_DEVICE_PORT, 55556)
                        val device = Device(name, ip, port, "Windows", DeviceStatus.CONNECTED)
                        isConnected = true
                        connectedDevice = device
                        addOrUpdateDevice(device)
                        statusMessage = "Connected to $name"
                    } else {
                        isConnected = false
                        connectedDevice = null
                        statusMessage = "Disconnected"
                    }
                }
                EcosystemService.ACTION_CONNECTION_ERROR -> {
                    val error = intent.getStringExtra(EcosystemService.EXTRA_ERROR) ?: ""
                    statusMessage = "Error: $error"
                    Toast.makeText(this@MainActivity, error, Toast.LENGTH_LONG).show()
                }
            }
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        registerServiceReceiver()
        checkPermissions()

        setContent {
            DeviceEcosystemTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    DeviceListScreen(
                        devices = devices,
                        isConnected = isConnected,
                        connectedDevice = connectedDevice,
                        statusMessage = statusMessage,
                        onScanQr = { scanQrCode() },
                        onConnectDevice = { device -> connectToDevice(device) }
                    )
                }
            }
        }
    }

    private fun registerServiceReceiver() {
        val filter = IntentFilter().apply {
            addAction(EcosystemService.ACTION_DEVICE_DISCOVERED)
            addAction(EcosystemService.ACTION_DEVICE_LOST)
            addAction(EcosystemService.ACTION_CONNECTION_STATUS_CHANGED)
            addAction(EcosystemService.ACTION_MESSAGE_RECEIVED)
            addAction(EcosystemService.ACTION_CONNECTION_ERROR)
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            registerReceiver(serviceReceiver, filter, RECEIVER_NOT_EXPORTED)
        } else {
            registerReceiver(serviceReceiver, filter)
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        try {
            unregisterReceiver(serviceReceiver)
        } catch (_: Exception) {}
    }

    private fun addOrUpdateDevice(device: Device) {
        deviceMap[device.ipAddress] = device
        refreshDeviceList()
    }

    private fun refreshDeviceList() {
        devices = deviceMap.values.toList()
    }

    private fun checkPermissions() {
        startEcosystemService()
    }

    private fun ensureCameraPermissionForScan() {
        if (ContextCompat.checkSelfPermission(
                this, Manifest.permission.CAMERA
            ) != PackageManager.PERMISSION_GRANTED
        ) {
            requestPermissionLauncher.launch(Manifest.permission.CAMERA)
        }
    }

    private fun startEcosystemService() {
        try {
            val intent = Intent(this, EcosystemService::class.java).apply {
                action = EcosystemService.ACTION_START
            }
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.O) {
                startForegroundService(intent)
            } else {
                startService(intent)
            }
        } catch (e: Exception) {
            android.util.Log.e("MainActivity", "Failed to start service", e)
            statusMessage = "Service failed: ${e.message}"
        }
    }

    private fun scanQrCode() {
        ensureCameraPermissionForScan()
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA)
            != PackageManager.PERMISSION_GRANTED
        ) {
            statusMessage = "Grant camera permission to scan QR."
            return
        }
        val options = ScanOptions().apply {
            setPrompt("Scan QR code to connect")
            setBeepEnabled(true)
            setOrientationLocked(true)
        }
        qrScannerLauncher.launch(options)
    }

    private fun handleQrScan(content: String) {
        try {
            val parts = content.split(":")
            if (parts.size >= 3) {
                val ip = parts[0]
                val port = parts[1].toIntOrNull() ?: 55556
                val name = parts[2]
                val device = Device(name, ip, port, "Windows", DeviceStatus.CONNECTING)

                addOrUpdateDevice(device)
                statusMessage = "Connecting to $name..."
                connectToDevice(device)
            } else {
                statusMessage = "Invalid QR: $content"
            }
        } catch (e: Exception) {
            statusMessage = "QR error: ${e.message}"
        }
    }

    private fun connectToDevice(device: Device) {
        try {
            val intent = Intent(this, EcosystemService::class.java).apply {
                action = EcosystemService.ACTION_CONNECT
                putExtra(EcosystemService.EXTRA_DEVICE_IP, device.ipAddress)
                putExtra(EcosystemService.EXTRA_DEVICE_PORT, device.port)
                putExtra(EcosystemService.EXTRA_DEVICE_NAME, device.name)
            }
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.O) {
                startForegroundService(intent)
            } else {
                startService(intent)
            }
        } catch (e: Exception) {
            statusMessage = "Connect failed: ${e.message}"
        }
    }
}

@Composable
fun DeviceListScreen(
    devices: List<Device>,
    isConnected: Boolean,
    connectedDevice: Device?,
    statusMessage: String,
    onScanQr: () -> Unit,
    onConnectDevice: (Device) -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Text(
            text = "Device Ecosystem",
            style = MaterialTheme.typography.headlineMedium,
            fontWeight = FontWeight.Bold
        )

        if (statusMessage.isNotEmpty()) {
            Text(
                text = statusMessage,
                style = MaterialTheme.typography.bodyMedium,
                color = when {
                    statusMessage.startsWith("Connected") -> Color(0xFF4CAF50)
                    statusMessage.startsWith("Error") || statusMessage.startsWith("Connect failed") -> Color(0xFFF44336)
                    else -> MaterialTheme.colorScheme.onSurface
                },
                modifier = Modifier.padding(vertical = 4.dp)
            )
        }

        Spacer(modifier = Modifier.height(8.dp))

        if (isConnected && connectedDevice != null) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.primaryContainer
                )
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        text = "Connected to ${connectedDevice.name}",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = connectedDevice.getEndpoint(),
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }
            Spacer(modifier = Modifier.height(16.dp))
        }

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = "Discovered Devices",
                style = MaterialTheme.typography.titleMedium
            )
            Button(onClick = onScanQr) {
                Text("Scan QR")
            }
        }

        Spacer(modifier = Modifier.height(8.dp))

        if (devices.isEmpty()) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = "No devices found.\nTap 'Scan QR' to connect",
                    style = MaterialTheme.typography.bodyLarge,
                    color = Color.Gray
                )
            }
        } else {
            LazyColumn(modifier = Modifier.weight(1f)) {
                items(devices) { device ->
                    DeviceItem(
                        device = device,
                        onConnect = { onConnectDevice(device) }
                    )
                }
            }
        }
    }
}

@Composable
fun DeviceItem(
    device: Device,
    onConnect: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = device.name,
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "${device.platform} - ${device.getEndpoint()}",
                    style = MaterialTheme.typography.bodySmall
                )
                Text(
                    text = device.status.name,
                    style = MaterialTheme.typography.bodySmall,
                    color = when (device.status) {
                        DeviceStatus.CONNECTED -> Color(0xFF4CAF50)
                        DeviceStatus.CONNECTING -> Color(0xFFFF9800)
                        DeviceStatus.ERROR -> Color(0xFFF44336)
                        else -> Color.Gray
                    }
                )
            }
            if (device.status != DeviceStatus.CONNECTED) {
                Button(onClick = onConnect) {
                    Text("Connect")
                }
            }
        }
    }
}
