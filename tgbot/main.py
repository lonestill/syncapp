import asyncio
import os
from aiogram import Bot, Dispatcher, Router
from aiogram.filters import Command
from aiogram.types import Message

router = Router()





@router.message(Command("start"))
async def cmd_start(message: Message) -> None:
    await message.answer(
        "Привет.\n"
        "Я простой учебный бот.\n"
        "Напиши /help."
    )

@router.message(Command("help"))
async def cmd_help(message: Message) -> None:
    await message.answer(
        "Как это работает:\n"
        "- Ты пишешь сообщение\n"
        "- Бот получает его как update\n"
        "- Код ниже смотрит текст и отвечает\n\n"
        "Команды: /start, /help\n"
        "Любой другой текст -> бот повторит его."
    )



@router.message(Command("ping"))
async def cmd_ping(message: Message) -> None:
    await message.answer("pong")

@router.message()
async def handle_message(message: Message) -> None:
    if not message.text:
        return

    text = message.text.strip()
    await message.answer(f"команда не найдена")


async def main() -> None:
    token = "8774229409:AAF8W9tFoCpUQoIl6UKkWNeDq6YduL2lUwA"
    bot = Bot(token=token)
    dp = Dispatcher()
    dp.include_router(router)

    print("Bot started. Press Ctrl+C to stop.")
    await dp.start_polling(bot)


asyncio.run(main())
