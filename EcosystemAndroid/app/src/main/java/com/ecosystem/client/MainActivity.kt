package com.ecosystem.client

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    ClientScreen()
                }
            }
        }
    }
}

@Composable
fun ClientScreen() {
    var ipAddress by remember { mutableStateOf("192.168.1.100") }
    var port by remember { mutableStateOf("8080") }
    var message by remember { mutableStateOf("Hello from Pixel!") }
    var serverResponse by remember { mutableStateOf("") }
    var isSending by remember { mutableStateOf(false) }

    val coroutineScope = rememberCoroutineScope()

    Column(modifier = Modifier.padding(16.dp)) {
        OutlinedTextField(
            value = ipAddress,
            onValueChange = { ipAddress = it },
            label = { Text("PC IP Address") },
            modifier = Modifier.fillMaxWidth()
        )
        Spacer(modifier = Modifier.height(8.dp))
        OutlinedTextField(
            value = port,
            onValueChange = { port = it },
            label = { Text("Port") },
            modifier = Modifier.fillMaxWidth()
        )
        Spacer(modifier = Modifier.height(8.dp))
        OutlinedTextField(
            value = message,
            onValueChange = { message = it },
            label = { Text("Message") },
            modifier = Modifier.fillMaxWidth()
        )
        Spacer(modifier = Modifier.height(16.dp))

        Button(
            onClick = {
                val parsedPort = port.toIntOrNull() ?: 8080
                isSending = true
                coroutineScope.launch {
                    serverResponse = TcpClient.sendTcpMessage(ipAddress, parsedPort, message)
                    isSending = false
                }
            },
            modifier = Modifier.fillMaxWidth(),
            enabled = !isSending
        ) {
            Text(if (isSending) "Sending..." else "Connect & Send")
        }

        Spacer(modifier = Modifier.height(16.dp))
        Text(text = "Server Response:\n$serverResponse", style = MaterialTheme.typography.bodyLarge)
    }
}
