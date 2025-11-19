from pwn import remote, context # pip install pwntools
from time import sleep
from random import random

context.log_level = 'debug'
r = remote('127.0.0.1', 40815)
while True:
    line = r.recvline().strip()
    if line:
        delayMillis = random() * 100
        print(f"Delaying for {delayMillis:.2f} ms")
        sleep(delayMillis / 1000)
        r.sendline(line)

# 该脚本模拟 Kyouko 连接本机 Mystia，并在每次接收到 Mystia 的数据后，随机一定延迟再发送回去
# 以此可以测试 Mystia/Kyouko 运动数据在面对网络延迟时的表现