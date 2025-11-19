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