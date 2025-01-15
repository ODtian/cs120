# CS120 Project Report

## Abstract

In this four-phase project for CS120, we implement a toy network with acoustic signal to transmit information through sound card. These four phase can be roughly summarized as the Physical layer, the MAC layer, the IP layer and the Application layer. We employ C# with NAudio NuGet packet to complete the whole project, because C# is more user-friendly than C++ and offers better performance than JAVA and Python. As for NAudio NuGet packet, it provides a rich range of support for different APIs, e.g. ASIO and WASAPI which we use.

## Project 0&1: Acoustic Link

Project 0 offers us a chance to learn about how to code with the sound card, which tests our ability of searching for and reading official documents and demo. Project 1 is the footstone of the athernet project, in which we are asked to implement the physical layer of the athernet with the external speaker. Specifically, we utilize WASAPI instead of ASIO as provided in this two project, because WASAPI is built into Windows, providing native support for audio without requiring additional drivers, making it more accessible than ASIO. We will discuss the implementation of the physical layer in the following order.

### Modulation and Demodulation

We implement the modulation with PSK as provided in the example matlab code. PSK is more suitable for this task because the open environment will lead to unnecessary and thorny challenges to handle the noise and amplitude for FSK and ASK. 

As for PSK, we should handle the problem of aligning with preamble signal. We use chirp signal as preamble and implement an async task of demodulator to monitor the preamble. The demodulator needs to maintain a small buffer to store the received sample and calculate whether it is a preamble by dot-multiplying and comparing with the threshold. If the product is over the threshold, this moment is marked as the starting point of the data. For demodulating of the data, we calculate the dot product with the symbol of 0, and if the result is over 0, then it is 0, otherwise 1. 

We fix the length of the payload in the packet and pad zeros, so it doesn't contain the length field. CRC32 is added to check whether there is any error in the demodulated signal.

### Error Correction

Besides the CRC32, to enhance the dependability of our acoustic link, we employ Reed-Solomon encoding to each packet. Each packet contains 32 bytes payload and 5 bytes ECC, and so it allows the received packet contain 2 errors.

As observed, the transmission may fail when the data is continuously same, so we also try to implement a 4B5B encoding, but we don't adopt it for the low cost performance. Instead we design an algorithm to handle this problem, we use the same random seed to generate the same random number on both sender and receiver, and then employ XOR to process the data.

### Higher Bandwidth

We use OFDM to improve the bandwidth of our toy network, which utilizes two orthogonal carriers overlapping without interfering with each other. This allows for high spectral efficiency and resistance to interference, making it ideal for high-speed data transmission.

### Challenges and Suggestions

Since this project utilizes external sound signal to transmit, the performance is subjected to the device and environment to a certain extend. A good speaker and microphone can help, while the built-in one in laptop behaves terrible. A quiet and open environment is required to determine the parameters such as frequency and threshold, and the narrow dormitory is not a good choice.

## Project 2: Manage Multiple Access

In this project, we implement the MAC layer. To be more specific, it mainly involves the sliding window algorithm and the CSMA state machine. Wired connection through USB sound card and audio cables. For the sake of performance, we switch to ASIO API instead of  WASAPI, which provide more flexible access to twisting the buffer.

### Sliding Window Algorithm

The sliding window algorithm is designed to manage the reliable transmission of data packets between two endpoints using a window-based flow control mechanism, and is nothing new than the one in class. A sequence number field is added to each packet, and note that the bits for sequence number field should cover the send window and receive window to avoid overlap. 

We use an array of bool to keep track of the received ack and an array of byte for received data. On the sender side, it monitors the sequence numbers of the frames within the window and retransmits if acknowledgements are not received within a certain timeout period. On the receiver side, the algorithm processes incoming frames, checking their sequence numbers to ensure they are within the expected range. If a frame is received out of order, it is stored in a buffer until the missing frames are received. Once the expected frame is received, the receiver sends an acknowledgment back to the sender and processes the buffered frames in order.

Although we successfully implement the sliding window algorithm, we set the window size as 1 which makes it equivalent to a stop-and-wait mechanism due to the low-bandwidth and high-noise environment, and such a frame-by-frame acknowledgment can better service the integrity of data transmission.

### CSMA

We implement the CSMA/CD protocol by sensing the communication medium before attempting to transmit data. If the medium is detected to be idle, the transmission proceeds; otherwise, the transmission is deferred to avoid collisions. We incorporates a mechanism to detect the presence of a carrier signal, which indicates whether the medium is currently in use. This is achieved through a carrier sensing component that continuously monitors the medium's energy levels. If the energy level exceeds a certain threshold, it indicates that the medium is busy, and the transmission is postponed. This helps in minimizing the chances of data collisions.

In addition to carrier sensing, the design includes a backoff algorithm to handle situations where the medium is busy. When a collision is detected or the medium is found to be busy, the transmission is delayed for a certain backoff period. This backoff period is calculated similar to what we learn as adaptive timeout in class, which adjust timeout according to the estimated RTT and optimize the utilization of resource. The estimated RTT is calculate with smooth average and it will increase a fix value if timeout.

### Challenges and Suggestions

This project must be the most time-consuming one due to the dependence of the devices. Here're some ridiculous problem we encountered, which takes us mass of time to find out. The provided devices including the sound card and cable may be broken, so change one if you can't find bug in your code, but it doesn't perform well. The actual rate of the USB port may not be consistent with the one you set in the Windows setting, so try print the rate before debugging. A higher frequency of the carrier wave can help because of the noisy environment. At the same time, we incorporate a warmup preamble to alleviate the influence of the device start up.

One tricky problem also puzzles us a lot, that is we don't know whether the audio API is blocked or not, so it's hard to set the backoff time. Chances are that the timeout happens before the packet is retrieved from the buffer and actually sent. In this case, the sender endpoint will keep plugging the same packet to the sender buffer multiple times, no matter whether it's received successfully or not, which waste a lot of time.

Unfortunately, we failed to pass task 4: CSMA with Interference in time. We conclude two reasons. The latency makes the carrier sense less effective. On the other hand, the packet size should balance between fitting in the idle interval of the jamming and making better use of the bandwidth. Actually, we can pass the task 4 if we set all the mixer volume to half, but we can't explain why it doesn't work when we maximize the volume.

All the parameters for project 1&2 are given in the very front of Program.cs for easy finetuning and reference.

## Project 3: To the Internet

This project requires us to implement some IP layer protocols on the top of the previous physical and MAC layer, including ICMP echo, router and NAT. In the end of this project, our toy network can be integrated into a broader network, leveraging IP layer for effective inter-network communication.

### ICMP Echo

The ICMP echo, or known as the ping operation, is implemented by detecting and processing ICMP packets within the network. We utilize the PacketDotnet to construct the packets. The key point is to provide a mechanism for network diagnostics, allowing devices to check connectivity and measure round-trip time. When an ICMP packet is identified, we checks whether it is an echo request. If yes, an echo reply packet is constructed and sent back to the source. This process involves capturing the incoming ICMP packet, verifying its type, and then generating a corresponding reply. 

### Router

The router functionality is achieved by examining the destination address of incoming packets and forwarding them to the next hop based on predefined routing rules. We capture the packets, inspect their IP headers, and then determine whether the destination address matches a specific criterion. If yes, the packet is forwarded to the next hop, which could be another router or the final destination. In this way, we enable the interconnection of different network segments, allowing data to travel from one network to another effectively.

### NAT

Network Address Translation(NAT) is realized through the manipulation of TCP packet headers, specifically the sequence and acknowledgement numbers. We initializes the sequence number from an incoming packet and then adjusts the sequence and acknowledgment numbers for outgoing and incoming packets, respectively. The principle behind NAT is to allow multiple devices on a local network to share a single public IP address, translating private IP addresses to a public one for outgoing traffic and vice versa for incoming traffic. This helps in conserving public IP addresses and provides an additional layer of security by hiding internal network structures. Here, we leverages the WinTun library to create the virtual network interface, which is easy-to-use and also high-performance.

### Challenges and Suggestions

Since this project, the influence of the physical device becomes minor, and it's a good news that we don't need to take too much effort to twist with the unreliable devices and tune the parameters. The challenges lies in ensuring the effective implementation of the MAC layer and understanding the principle of these IP layer protocols. 

## Project 4: Above IP

We dive into the application layer in this project, and revisit the IP layer implementation to support DNS and HTTP.

### DNS

The DNS protocol is implemented to resolve domain names to IP addresses. This involves sending a DNS query to a DNS server and processing the response to extract the IP address. We utilize the ARSoft.Tools.Net.Dns library to handle the DNS queries and responses. The implementation involves creating a DNS client that constructs a DNS query packet, sends it to the DNS server, and waits for the response. Upon receiving the response, the client parses the packet to extract the IP address associated with the queried domain name.

## HTTP

We implement a SeqHijack class to manipulates TCP sequence and acknowledgment numbers to maintain the integrity of TCP connections. This is crucial for establishing and maintaining a reliable connection with HTTP servers. We use WinTun.Adapter to set up a network adapter for communication. It uses the SeqHijack class to adjust TCP sequence numbers for packets, ensuring proper synchronization. The method processes incoming packets, identifies HTTP traffic and manipulates TCP headers.
