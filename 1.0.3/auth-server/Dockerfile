FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PYTHONPATH=/app/shared

WORKDIR /app

COPY auth-server/requirements.txt /tmp/netrix-auth-requirements.txt
RUN pip install --no-cache-dir -r /tmp/netrix-auth-requirements.txt

COPY shared ./shared
COPY auth-server ./auth-server

WORKDIR /app/auth-server

EXPOSE 8001

CMD ["python", "app.py"]
