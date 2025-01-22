# Aether.NET - A CS120: Computer Network Project in C#

We implment an 3-layer acoustic link with C# (.NET 9) and ASIO or WASAPI, including PHY, MAC and IP. C# is good at high level abstraction and low level control, and also flexibility (you will notice some `dynamic` vars in our code).

## Completion
- Project 0. Warm Up [3/3]
  - - [x] Task 1: (0 point) Self-assessment
  - - [x] Task 2: (3 points) Understanding Your Tools
- Project 1. Acoustic Link [10/10 + Optinal 5/8]
  - - [x] Task 1: (3 points) Understanding Your Tools
  - - [x] Task 2: (2 points) Generating Sound Waves at Will
  - - [x] Task 3: (5 points) Transmitting Your First Bit
  - - [x] Task 4: (Optional, 1 point) Error Correction
  - - [x] Task 5: (Optional, 2 points) OFDM
  - - [ ] Task 6: (Optional, 2 points) Chip Dream
  - - [ ] Task 7: (Optional, 1 point) MIMO
  - - [x] (2/2) Task 8: (Optional, 2 points) Range Challenge
- Project 2. Manage Multiple Access [11/12 + Optinal 6/6]
  - - [x] Task 0: (0 point) Audio Toolkit
  - - [x] Task 1: (4 points) Cable Connection
  - - [x] Task 2: (5 points) Acknowledgement
  - - [x] Task 3: (2 points) Carrier Sense Multiple Access
  - - [ ] Task 4: (1 point) CSMA with Interference
  - - [x] (3/3) Task 5: (Optional, 3 points) Performance Rank
  - - [x] Task 6: (Optional, 3 points) X
- Project 3. To the Internet [10/10 + Optinal 4.5/*]
  - - [x] Task 0: (0 point) Sending and Receiving IP Datagram
  - - [x] Task 1: (3 points) ICMP Echo
  - - [x] Task 2: (4 points) Router
  - - [x] Task 3: (3 points) NAT
  - - [ ] Task 4: (Optional, 1 point) NAT Traversal
  - - [ ] Task 5: (Optional, 1 point) IP Fragmentation
  - - [x] Task 6: (Optional, 0 point) Virtual Network Device
  - - [x] Task 7: (Optional, 1 point) ICMP Echo #
  - - [x] Task 8: (Optional, 1 point) Router #
  - - [x] Task 9: (Optional, 1 point) NAT #
  - - [ ] Task 10: (Optional, 1 point) ARP
  - - [x] (1.5/*) Task 11: (Optional, * points) Star
- Project 4. Above IP [8/8 + Optinal 2/2]
  - - [x] Task 1: (4 points) DNS
  - - [x] Task 2: (1 points) HTTP
  - - [x] Task 3: (3 points) Project Report
  - - [x] Part 4. (0 point) Relaunch
  - - [x] Part 5. (Optional, 1 point) Browsing the Web
  - - [x] Part 6. (Optional, 1 point) Project Report #

## For new to the project
- Buy a good speaker and microphone before you start project 1. Mono is recommanded.
- Volume is the most important part in this project, adjust and find your best volume before you start implement the project! Be careful that higher volume is not always better due to the low quality of provided sound cards.
- A virtual interface combined with Winodws integrated features can significantly reduce the work in project 3&4, however you won't be able to finish some optional tasks like NAT Traversal or IP Fragmentation unless using your own NAT implemention.
- You will find these command useful:
  - `New-NetNat`: Setup a NAT in Windows.
  - `New-NetIPAddress`: Setup virtual interface IP address, it will also setup route table.
  - `Set-NetIpInterface -Forwarding`: Enable packet forwarding for the interface, open it to pass Star task. Also reminber disabling the NAT.
- WSL provides mirrored interface, while Linux's `ping` and `curl` provide options to use certain interface. It is useful to debug your program without disconnecting other links.